namespace NetStucked.Core;

public enum TerminalHostKeyDecision { Reject, AcceptOnce, AcceptAndSave }
public sealed record TerminalKnownHost(string Host, int Port, string Algorithm, string Fingerprint, DateTimeOffset AcceptedAt)
{
    public string Endpoint => Host.Contains(':') ? $"[{Host}]:{Port}" : $"{Host}:{Port}";
}
public sealed record TerminalHostKeyChallenge(TerminalKnownHost Presented, TerminalKnownHost? Previous)
{
    public bool Changed => Previous is not null;
}
public interface ITerminalHostKeyPrompt
{
    Task<TerminalHostKeyDecision> ConfirmAsync(TerminalHostKeyChallenge challenge, CancellationToken token);
}
public interface ITerminalKnownHostStore
{
    Task<IReadOnlyList<TerminalKnownHost>> LoadAsync(CancellationToken token);
    Task<bool> SaveAsync(TerminalKnownHost host, string? expectedPreviousFingerprint, CancellationToken token);
    Task<bool> RemoveAsync(TerminalKnownHost host, CancellationToken token);
}
public interface ITerminalHostTrust
{
    Task<bool> VerifyAsync(string host, int port, string algorithm, string fingerprint, CancellationToken token);
}
