using System.Net;
using System.Text;
using NetStucked.Core;
using NetStucked.Infrastructure;
using Xunit;

namespace NetStucked.Tests;

public class ReleaseTests
{
    [Theory]
    [InlineData("1.2.3",true)][InlineData("0.2.0",true)][InlineData("1.0.0-rc.12+sha",true)]
    [InlineData("01.0.0",false)][InlineData("1.0",false)][InlineData("1.0.0-01",false)][InlineData("v1.0.0",false)][InlineData("1.0.0<script>",false)]
    public void UsesStrictSemver(string text,bool valid)=>Assert.Equal(valid,SemanticVersion.TryParse(text,out _));
    [Fact]
    public void PrereleaseAndMetadataUseSemverPrecedence()
    {
        string[] ordered=["1.0.0-alpha","1.0.0-alpha.1","1.0.0-alpha.beta","1.0.0-beta","1.0.0-beta.2","1.0.0-beta.11","1.0.0-rc.1","1.0.0","1.1.0","2.0.0"];
        var values=ordered.Select(v=> {SemanticVersion.TryParse(v,out var parsed);return parsed!;}).ToArray();
        for(int i=1;i<values.Length;i++) Assert.True(values[i].CompareTo(values[i-1])>0);
        SemanticVersion.TryParse("1.0.0+build",out var metadata); Assert.Equal(0,values[7].CompareTo(metadata));
    }
    [Fact]
    public async Task RealRepositoryRequestHasBoundedPublicReadAndDoesNotInventEmptyRelease()
    {
        var handler=new Handler("[]"); using var source=new GitHubReleaseSource(handler);
        var result=await source.CheckAsync(CancellationToken.None); Assert.Empty(result.Releases);
        Assert.Equal("api.github.com",handler.Url!.Host); Assert.Contains("pk-wrk-sea/NetStucked/releases",handler.Url.AbsolutePath);
        Assert.True(handler.HasUserAgent); Assert.False(handler.HasAuthorization);
    }
    [Fact]
    public async Task FiltersDraftPrereleaseUnsafeLinksAndSortsActualVersions()
    {
        string json="["+Row("v0.3.0")+","+Row("v0.2.1")+","+Row("v9.0.0",draft:true)+","+Row("v8.0.0",pre:true)+","+Row("v7.0.0",url:"https://evil.invalid/download")+","+Row("v6.0.0-rc.1")+"]";
        using var source=new GitHubReleaseSource(new Handler(json)); var result=await source.CheckAsync(CancellationToken.None);
        Assert.Equal(2,result.Releases.Count); Assert.Equal("0.3.0",result.Releases[0].Version.ToString()); Assert.NotNull(result.Releases[0].InstallerPage);
        Assert.Equal("actual release notes",result.Releases[0].Notes);
    }
    [Theory]
    [InlineData(404)][InlineData(403)][InlineData(429)][InlineData(500)]
    public async Task ApiFailuresAreNotReportedAsUpToDate(int status)
    {
        using var source=new GitHubReleaseSource(new Handler("[]",(HttpStatusCode)status));
        await Assert.ThrowsAsync<HttpRequestException>(()=>source.CheckAsync(CancellationToken.None));
    }
    [Fact]
    public async Task RejectsMalformedAndOversizedResponses()
    {
        using var malformed=new GitHubReleaseSource(new Handler("{}")); await Assert.ThrowsAsync<InvalidDataException>(()=>malformed.CheckAsync(CancellationToken.None));
        using var large=new GitHubReleaseSource(new Handler(new string(' ',2000001))); await Assert.ThrowsAsync<InvalidDataException>(()=>large.CheckAsync(CancellationToken.None));
    }
    [Theory]
    [InlineData("https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.2.0",true)]
    [InlineData("https://github.com/other/NetStucked/releases/tag/v0.2.0",false)]
    [InlineData("http://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.2.0",false)]
    [InlineData("https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.2.0?redirect=evil",false)]
    public void ExternalActionsAreConfinedToProjectReleasePages(string url,bool allowed)=>Assert.Equal(allowed,ReleaseRepository.IsReleasePage(new(url)));
    private static string Row(string tag,bool draft=false,bool pre=false,string? url=null)=>System.Text.Json.JsonSerializer.Serialize(new {tag_name=tag,draft,prerelease=pre,html_url=url??$"https://github.com/pk-wrk-sea/NetStucked/releases/tag/{tag}",name=tag,body="actual release notes",published_at="2026-10-09T01:00:00Z",assets=new[]{new{name=$"NetStucked-{tag.TrimStart('v')}-win-x64-setup.exe"}}});
    private sealed class Handler(string body,HttpStatusCode status=HttpStatusCode.OK):HttpMessageHandler
    {
        public Uri? Url; public bool HasUserAgent,HasAuthorization;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Url=request.RequestUri; HasUserAgent=request.Headers.UserAgent.Count>0; HasAuthorization=request.Headers.Authorization is not null;
            return Task.FromResult(new HttpResponseMessage(status){Content=new StringContent(body,Encoding.UTF8,"application/json")});
        }
    }
}
