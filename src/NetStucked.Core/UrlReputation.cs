namespace NetStucked.Core;
public interface ISecretVault { string? Read(string reference); void Write(string reference, string secret); void Delete(string reference); }
public sealed record UrlReputationResult(string Url, string Status, int? Malicious = null, int? Suspicious = null, int? Harmless = null, int? Undetected = null, DateTimeOffset? LastAnalysis = null, string Details = "");
public interface IUrlReputationLookup { Task<UrlReputationResult> LookupAsync(Uri url, string apiKey, CancellationToken token); }
