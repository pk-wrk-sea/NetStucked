using System.Reflection;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

// Exercise the production pre-ShellStream gate with owned byte buffers. These
// checks complement the actual-wire pre-shell-success flood fixture.
[Collection("Exclusive timing and bulk fixtures")]
public sealed class CollectorSshBufferGateTests
{
    [Fact]
    public async Task SetupLimitIsCumulativeAcrossStdoutAndStderrAndRejectsBeforeEnqueue()
    {
        using var stop = new CancellationTokenSource(); using var gate = new GuardProbe(stop.Token);
        for (int i = 0; i < 16; i++) gate.Capture(new byte[4096], 0, 4096, i % 2 != 0);
        var failure = Assert.Throws<CollectorException>(() => gate.Capture([99], 0, 1, true));
        Assert.Equal(CollectorFailure.Transport, failure.Failure);
        Assert.Contains("64 KiB", failure.Message);
        gate.Complete(); Assert.Equal(65536, (await ReadAllAsync(gate)).Length);
    }

    [Fact]
    public async Task OversizedInitialPacketDoesNotPartiallyEnqueueOrConsumeSetupBudget()
    {
        using var stop = new CancellationTokenSource(); using var gate = new GuardProbe(stop.Token);
        byte[] excessive = new byte[65537]; Array.Fill(excessive, (byte)'s');
        var failure = Assert.Throws<CollectorException>(() => gate.Capture(excessive, 0, excessive.Length, false));
        Assert.DoesNotContain(new string('s', 20), failure.Message);
        gate.Capture([7, 8, 9], 0, 3, false); gate.Complete();
        Assert.Equal(new byte[] { 7, 8, 9 }, await ReadAllAsync(gate));
    }

    [Fact]
    public async Task CaptureHonorsByteSliceAndCopiesItBeforeCallerReusesPacket()
    {
        using var stop = new CancellationTokenSource(); using var gate = new GuardProbe(stop.Token);
        byte[] packet = [90, 65, 66, 67, 91]; gate.Capture(packet, 1, 3, false);
        Array.Fill(packet, (byte)0); gate.Complete();
        Assert.Equal(new byte[] { 65, 66, 67 }, await ReadAllAsync(gate));
    }

    [Fact]
    public async Task AttachmentDrainsSdkDuplicateAndAllowsMoreThanSetupLimitAfterward()
    {
        using var stop = new CancellationTokenSource(); using var gate = new GuardProbe(stop.Token);
        int duplicateBytes = 9000, drained = 0, calls = 0;
        gate.AttachDrain(() => duplicateBytes > 0, (_, _, count) =>
        {
            int used = Math.Min(count, duplicateBytes); duplicateBytes -= used; drained += used; calls++; return used;
        });
        Assert.Equal(9000, drained); Assert.Equal(3, calls);
        byte[] block = Enumerable.Repeat((byte)'x', 4096).ToArray();
        for (int i = 0; i < 24; i++) gate.Capture(block, 0, block.Length, i % 2 != 0);
        gate.Complete(); byte[] collected = await ReadAllAsync(gate);
        Assert.Equal(96 * 1024, collected.Length); Assert.All(collected, b => Assert.Equal((byte)'x', b));
    }

    [Fact]
    public async Task ConcurrentDrainsSerializeAvailabilityAndReadAsOneOperation()
    {
        using var stop = new CancellationTokenSource(); using var gate = new GuardProbe(stop.Token);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int available = 0, reads = 0;
        gate.AttachDrain(() => available > 0, (_, _, _) =>
        {
            Interlocked.Increment(ref reads); entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("Owned drain barrier expired.");
            Assert.Equal(1, available); available = 0; return 1;
        });
        available = 1;
        Task first = Task.Run(gate.Drain); Task? second = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            second = Task.Run(() => { secondStarted.SetResult(); gate.Drain(); });
            await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(2)); await Task.Delay(50);
            Assert.False(second.IsCompleted, "The second observer must wait until the first finishes availability plus Read.");
            release.Set(); await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(1, reads);
        }
        finally { release.Set(); await first; if (second is not null) await second; }
    }

    [Fact]
    public async Task EmptySdkReadEndsEachDrainWithoutSpinningOrBlocking()
    {
        using var stop = new CancellationTokenSource(); using var gate = new GuardProbe(stop.Token);
        int reads = 0;
        await Task.Run(() => gate.AttachDrain(() => true, (_, _, _) => { reads++; return 0; })).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, reads);
        await Task.Run(gate.Drain).WaitAsync(TimeSpan.FromSeconds(2)); Assert.Equal(2, reads);
    }

    [Fact]
    public async Task ReadyGuardKeepsCombinedQueueBoundedAndCancellationReleasesBlockedCapture()
    {
        using var stop = new CancellationTokenSource(); using var gate = new GuardProbe(stop.Token);
        gate.AttachDrain(() => false, (_, _, _) => throw new InvalidOperationException("No SDK duplicate bytes exist."));
        byte[] block = new byte[4096];
        for (int i = 0; i < 32; i++) gate.Capture(block, 0, block.Length, i % 2 != 0);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task blocked = Task.Run(() => { started.SetResult(); gate.Capture(block, 0, block.Length, true); });
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(2)); await Task.Delay(50);
            Assert.False(blocked.IsCompleted, "Capture 33 must apply backpressure across both output streams.");
            stop.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blocked.WaitAsync(TimeSpan.FromSeconds(2)));
        }
        finally { stop.Cancel(); gate.Complete(); try { await blocked; } catch (OperationCanceledException) { } }
    }

    private static async Task<byte[]> ReadAllAsync(GuardProbe gate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3)); using var collected = new MemoryStream();
        byte[] buffer = new byte[3000]; int count;
        while ((count = await gate.ReadAsync(buffer, deadline.Token)) > 0) collected.Write(buffer, 0, count);
        return collected.ToArray();
    }
    private sealed class GuardProbe : IDisposable
    {
        private readonly Action<byte[], int, int, bool> _capture;
        private readonly Action<Func<bool>, Func<byte[], int, int, int>> _attach;
        private readonly Action _drain;
        private readonly Action<Exception?> _complete;
        private readonly Func<Memory<byte>, CancellationToken, ValueTask<int>> _read;
        public GuardProbe(CancellationToken stop)
        {
            Assembly assembly = typeof(CollectorTransport).Assembly;
            Type outputType = assembly.GetType("NetStucked.Infrastructure.CollectorSshOutput", true)!;
            object output = Activator.CreateInstance(outputType, stop)!;
            _complete = outputType.GetMethod("Complete")!.CreateDelegate<Action<Exception?>>(output);
            _read = outputType.GetMethod("ReadAsync")!.CreateDelegate<Func<Memory<byte>, CancellationToken, ValueTask<int>>>(output);
            Type guardType = assembly.GetType("NetStucked.Infrastructure.CollectorShellGuard", true)!;
            object guard = Activator.CreateInstance(guardType, output)!;
            _capture = guardType.GetMethod("Capture")!.CreateDelegate<Action<byte[], int, int, bool>>(guard);
            _attach = guardType.GetMethod("AttachDrain")!.CreateDelegate<Action<Func<bool>, Func<byte[], int, int, int>>>(guard);
            _drain = guardType.GetMethod("Drain")!.CreateDelegate<Action>(guard);
        }
        public void Capture(byte[] data, int offset, int count, bool error) => _capture(data, offset, count, error);
        public void AttachDrain(Func<bool> available, Func<byte[], int, int, int> read) => _attach(available, read);
        public void Drain() => _drain();
        public void Complete() => _complete(null);
        public ValueTask<int> ReadAsync(Memory<byte> target, CancellationToken token) => _read(target, token);
        public void Dispose() => Complete();
    }
}
