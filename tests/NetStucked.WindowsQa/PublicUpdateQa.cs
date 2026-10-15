using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using NetStucked.Desktop.ViewModels;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;

internal static partial class Program
{
    // Runs against the actual application DLLs beside this QA executable, including older public builds.
    // No Install command is executed; the production checker and ViewModel decide eligibility.
    private static async Task<int> CheckPublicUpdateAsync(string output, string expectedCurrent, string expectedAvailable)
    {
        Directory.CreateDirectory(output);
        try
        {
            using var source = new GitHubReleaseSource();
            var store = new UserSettingsStore(Path.Combine(output, "isolated-user-data"));
            using var updater = new WindowsApplicationUpdateService(store);
            await using var vm = new UpdatesViewModel(source, new RecordingLinks(), updater);
            vm.StopDiagnosticsAsync = () => Task.CompletedTask;
            Require(vm.CurrentVersion == expectedCurrent, "Public check uses the requested real application version");
            Require(vm.Available is null, "No update is fabricated before checking GitHub");
            await vm.CheckCommand.ExecuteAsync(null);
            Require(vm.StatusTitle != "Unable to check for updates", "Actual public GitHub release request succeeds");
            Require(vm.Builds.Any(b => b.Installer is not null), "Public catalog contains eligible installers");
            if (expectedAvailable == "none")
            {
                Require(vm.Available is null && vm.StatusTitle == "No newer published version", "Latest application sees no newer public release");
                Require(vm.InstallLabel == "Reinstall v" + expectedCurrent, "Latest application selects same-version Reinstall");
                string previous = vm.History.First(h => h.Version != vm.CurrentVersion).Version;
                Require(vm.PreviousVersion == "Previous documented version: v" + previous, "Bundled history identifies the actual previous version");
                vm.SelectedBuild = vm.Builds.First(b => b.Version.ToString() == previous);
                Require(vm.InstallLabel == "Recover v" + previous && vm.InstallCommand.CanExecute(null), "Previous published build remains selectable for Recovery");
            }
            else
            {
                Require(vm.Available?.Version.ToString() == expectedAvailable, "Published baseline discovers the expected public update");
                Require(vm.SelectedBuild == vm.Available && vm.InstallLabel == "Install v" + expectedAvailable, "Real update is selected for installation");
                Require(!string.IsNullOrWhiteSpace(vm.Notes) && vm.Notes == vm.Available!.Notes, "Actual provider release notes are displayed");
            }
            Require(vm.InstallCommand.CanExecute(null), "Verified public installer metadata enables the explicit install action");
            var assembly = typeof(UpdatesViewModel).Assembly;
            await File.WriteAllTextAsync(Path.Combine(output, "public-update-check.json"), JsonSerializer.Serialize(new
            {
                Source = "Production GitHubReleaseSource and UpdatesViewModel from real published assemblies; no fixture",
                CurrentVersion = vm.CurrentVersion, AvailableVersion = vm.Available?.Version.ToString(),
                vm.StatusTitle, vm.Badge, vm.InstallLabel, vm.PreviousVersion, vm.LastChecked,
                AppAssembly = assembly.Location,
                AppSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))),
                Installer = vm.Available?.Installer ?? vm.SelectedBuild?.Installer,
                InstallerExecuted = false
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("PUBLIC UPDATE CHECK PASSED");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex); return 1; }
    }
}
