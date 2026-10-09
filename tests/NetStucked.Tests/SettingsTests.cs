using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public sealed class SettingsTests
{
    [Theory]
    [InlineData("{\"TargetText\":null}")]
    [InlineData("{\"TraceTarget\":null}")]
    [InlineData("{\"Columns\":{\"Trace\":[null]}}")]
    [InlineData("{\"TraceDescriptions\":{\"127.0.0.1:1\":null}}")]
    [InlineData("{\"PingTemplates\":null}")]
    [InlineData("{\"TraceHistory\":null}")]
    [InlineData("{\"TraceHistory\":[\"bad target\"]}")]
    [InlineData("{\"Ping\":{\"TimeoutMs\":-1}}")]
    public void NullSettingsValuesAreRejectedInsteadOfReachingTheUi(string json)
    {
        string directory = Path.Combine(Path.GetTempPath(), "NetStucked-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "settings.json"), json);
            var store = new UserSettingsStore(directory);
            Assert.NotNull(store.LoadError);
            Assert.Equal("", store.Preferences.TargetText);
            Assert.Equal("", store.Preferences.TraceTarget);
            Assert.Empty(store.Preferences.Columns);
            Assert.Empty(store.Preferences.TraceDescriptions);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task TemplatesHistoryAndStretchColumnsRoundTripAcrossSerializedSaves()
    {
        string directory = Path.Combine(Path.GetTempPath(), "NetStucked-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new UserSettingsStore(directory);
            var template = new PingTemplate(Guid.NewGuid(), "Branch ไทย", "10.1.1.1 Router\n20.3.3.3 Edge");
            store.Preferences.PingTemplates.Add(template);
            store.Preferences.TraceHistory.AddRange(["10.1.1.1", "example.test"]);
            store.Preferences.Columns["Ping"] = [new("ResultError", 18, 200, true, true)];
            var saves = new List<Task>();
            for (int i = 0; i < 20; i++) { store.Preferences.TraceTarget = $"192.0.2.{i + 1}"; saves.Add(store.SaveAsync()); }
            await Task.WhenAll(saves);
            var loaded = new UserSettingsStore(directory);
            Assert.Null(loaded.LoadError); Assert.Equal(template, Assert.Single(loaded.Preferences.PingTemplates));
            Assert.Equal(store.Preferences.TraceHistory, loaded.Preferences.TraceHistory);
            Assert.Equal("192.0.2.20", loaded.Preferences.TraceTarget); Assert.True(loaded.Preferences.Columns["Ping"][0].Stretch);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact]
    public void LegacyShortTimeoutsAreMigratedWhileOtherPreferencesRemainIntact()
    {
        string directory = Path.Combine(Path.GetTempPath(), "NetStucked-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{\"Ping\":{\"TimeoutMs\":100},\"Trace\":{\"TimeoutMs\":400},\"TargetText\":\"10.1.1.1\"}");
            var loaded = new UserSettingsStore(directory);
            Assert.Null(loaded.LoadError); Assert.Equal(500, loaded.Preferences.Ping.TimeoutMs); Assert.Equal(500, loaded.Preferences.Trace.TimeoutMs);
            Assert.Equal("10.1.1.1", loaded.Preferences.TargetText);
        }
        finally { Directory.Delete(directory, true); }
    }
}
