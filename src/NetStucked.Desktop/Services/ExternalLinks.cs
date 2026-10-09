using System.Diagnostics;
using NetStucked.Core;

namespace NetStucked.Desktop.Services;

public interface IExternalLinks { void OpenRelease(Uri page); }
public sealed class ExternalLinks : IExternalLinks
{
    public void OpenRelease(Uri page)
    {
        if (page != ReleaseRepository.ReleasesPage && !ReleaseRepository.IsReleasePage(page)) throw new ArgumentException("Only this project's GitHub release pages can be opened.");
        Process.Start(new ProcessStartInfo(page.AbsoluteUri) { UseShellExecute = true });
    }
}
