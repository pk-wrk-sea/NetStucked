using System.IO;
using Microsoft.Extensions.Logging;

namespace NetStucked.Desktop.Services;

/// <summary>Bounded queue; background writer keeps disk I/O off the Dispatcher.</summary>
public sealed class LocalLoggerProvider : ILoggerProvider
{
    private readonly System.Threading.Channels.Channel<string> _queue = System.Threading.Channels.Channel.CreateBounded<string>(
        new System.Threading.Channels.BoundedChannelOptions(256) { FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly Task _writer;
    public LocalLoggerProvider(string directory)
    {
        _writer = Task.Run(async () =>
        {
            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "app.log");
                await foreach (string line in _queue.Reader.ReadAllAsync())
                {
                    if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".previous", true);
                    await File.AppendAllTextAsync(path, line + Environment.NewLine);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { System.Diagnostics.Debug.WriteLine(ex); }
        });
    }
    public ILogger CreateLogger(string categoryName) => new LocalLogger(categoryName, _queue.Writer);
    public void Dispose() { _queue.Writer.TryComplete(); _writer.GetAwaiter().GetResult(); }
    private sealed class LocalLogger(string category, System.Threading.Channels.ChannelWriter<string> writer) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(level)) writer.TryWrite($"{DateTimeOffset.Now:O} {level} {category}: {formatter(state, exception)} {exception?.Message}");
        }
    }
}
