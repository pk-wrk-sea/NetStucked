using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using NetStucked.Core;
using System.Windows;

namespace NetStucked.Desktop.Services;

public interface IEngineeringFiles
{
    string? Open(string kind);
    string? Save(string kind, string name);
}
public sealed class EngineeringFiles : IEngineeringFiles
{
    private static string Filter(string kind) => kind switch { "xlsx" => "Excel workbook (*.xlsx)|*.xlsx", "evidence" => "Collector JSON or UTF-8 transcript (*.json;*.txt)|*.json;*.txt", "html" => "HTML report (*.html)|*.html", _ => "CSV report (*.csv)|*.csv" };
    public string? Open(string kind) { var d = new OpenFileDialog { Filter = Filter(kind), CheckFileExists = true }; return d.ShowDialog(Application.Current.MainWindow) == true ? d.FileName : null; }
    public string? Save(string kind, string name) { var d = new SaveFileDialog { Filter = Filter(kind), FileName = name, OverwritePrompt = true, AddExtension = true }; return d.ShowDialog(Application.Current.MainWindow) == true ? d.FileName : null; }
}
public partial class EngineeringContext(IEngineeringStore store) : ObservableObject, IAsyncDisposable
{
    public EngineeringData Data { get; private set; } = EngineeringData.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEdit))] private bool _isBusy;
    [ObservableProperty] private string _message = "Loading local engineering workspace…";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEdit))] private bool _loaded;
    public bool CanEdit => Loaded && !IsBusy;
    private CancellationTokenSource? _operationCancel;
    private bool _disposed;
    private Task? _active;
    public event Action? Changed;
    public Task InitializeAsync() => Loaded ? Task.CompletedTask : ReloadAsync();
    public Task ReloadAsync() => RunAsync(async token => { Data = await store.LoadAsync(token); Loaded = true; Message = $"Local workspace · revision {Data.Revision} · no network activity"; Changed?.Invoke(); });
    public Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy || _disposed) { Message = "Wait for the current operation or cancel it first."; return Task.CompletedTask; }
        IsBusy = true; _operationCancel = new(); _active = RunCoreAsync(action); return _active;
    }
    private async Task RunCoreAsync(Func<CancellationToken, Task> action)
    {
        try { await action(_operationCancel!.Token); }
        catch (OperationCanceledException) { Message = "Operation cancelled; previously saved data retained."; }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or System.Net.Http.HttpRequestException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException) { Message = "Operation failed: " + ex.Message; }
        finally { IsBusy = false; _operationCancel?.Dispose(); _operationCancel = null; }
    }
    public async Task CommitAsync(Func<EngineeringData, EngineeringData> change, CancellationToken token)
    {
        if (!Loaded) throw new InvalidOperationException("Workspace is not loaded. Reload first; original data retained.");
        Data = await store.SaveAsync(change(Data), Data.Revision, token); Changed?.Invoke();
        Message = $"Saved local workspace revision {Data.Revision}.";
    }
    public void Cancel() => _operationCancel?.Cancel();
    public async Task StopAsync() { Cancel(); if (_active is { } active) await active; }
    public async ValueTask DisposeAsync() { _disposed = true; await StopAsync(); }
}
