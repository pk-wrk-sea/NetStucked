using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using NetStucked.Core;
using NetStucked.Desktop.Views;
using NetStucked.Desktop.ViewModels;
using NetStucked.Infrastructure;
namespace NetStucked.WindowsQa;
internal static partial class Program
{
    private static int CheckPaloSite(string output)
    {
        Directory.CreateDirectory(output);int exit=0;var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("pack://application:,,,/NetStucked;component/Resources/Theme.xaml") });
        var view=new UrlCheckerView {DataContext=new UrlCheckerViewModel(new VirusTotalUrlLookup(),new QaSecretVault(),new UserSettingsStore(Path.Combine(output,"isolated-user-data")))};
        var window=new Window {Content=view,Width=1200,Height=900,ShowInTaskbar=false,ShowActivated=false,Opacity=0};window.Show();
        _=app.Dispatcher.InvokeAsync(async()=>
        {
            try
            {
                string runtime=CoreWebView2Environment.GetAvailableBrowserVersionString();Descendants<System.Windows.Controls.TabControl>(view).Single().SelectedIndex=1;await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);await view.OpenPaloAsync().WaitAsync(TimeSpan.FromSeconds(45));var browser=((System.Windows.Controls.ContentControl)view.FindName("BrowserHost")).Content as WebView2??throw new InvalidOperationException("Embedded browser did not initialize.");
                using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(60));string html="";
                while(!deadline.IsCancellationRequested) { try {html=await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({url:location.href,title:document.title,ready:document.readyState,text:document.body?.innerText})");await File.WriteAllTextAsync(Path.Combine(output,"page-state.json"),html);if(html.Contains("Test A Site")&&html.Contains("Search",StringComparison.OrdinalIgnoreCase))break;}catch(InvalidOperationException){}await Task.Delay(500,deadline.Token); }
                Require(html.Contains("Test A Site")&&html.Contains("Search",StringComparison.OrdinalIgnoreCase),"Actual embedded Palo Alto provider page loads with its search interface");
                await File.WriteAllTextAsync(Path.Combine(output,"page-controls.json"),await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify([...document.querySelectorAll(\u0027input,button\u0027)].map(e=>({tag:e.tagName,type:e.type,placeholder:e.getAttribute(\u0027placeholder\u0027),text:e.innerText,id:e.id})))"));
                await File.WriteAllTextAsync(Path.Combine(output,"form-info.json"),await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({hasForm:!!document.getElementById('id_url')?.form,method:document.getElementById('id_url')?.form?.method,action:document.getElementById('id_url')?.form?.action})"));
                await browser.CoreWebView2.ExecuteScriptAsync("(()=>{const input=document.getElementById('id_url');if(!input?.form)throw new Error('Observed provider form is missing');input.value='example.com';input.dispatchEvent(new Event('input',{bubbles:true}));input.dispatchEvent(new Event('change',{bubbles:true}));const button=[...input.form.querySelectorAll(\u0027button\u0027)].find(b=>b.innerText.trim().toUpperCase()===\u0027SEARCH\u0027);if(!button)throw new Error(\u0027Observed SEARCH button missing\u0027);button.click();})()");
                await Task.Delay(8000);
                string searchState=await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({url:location.href,title:document.title,text:document.body?.innerText})");
                await File.WriteAllTextAsync(Path.Combine(output,"search-page-state.json"),searchState);
                await using(var stream=File.Create(Path.Combine(output,"palo-test-a-site.png")))await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
                await File.WriteAllTextAsync(Path.Combine(output,"palo-site-check.json"),JsonSerializer.Serialize(new {Runtime=runtime,Url=browser.Source.AbsoluteUri,ProviderPageLoaded=true,CategorySearchAttempted=true,CategoryResultVerified=false,ProviderCaptchaRejected=searchState.Contains("captcha validation error",StringComparison.OrdinalIgnoreCase),Script="Provider's observed public form and SEARCH button; no API scrape or captcha bypass",KeyUsed=false},new JsonSerializerOptions {WriteIndented=true}));
            }
            catch(Exception ex){exit=1;Console.Error.WriteLine("FAIL "+ex);}
            finally {window.Close();app.Shutdown();}
        });app.Run();return exit;
    }
}
