using Ax.Fw;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using JustLogger.Interfaces;
using Newtonsoft.Json;
using Roadnik.Common.ReqRes;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Resources.Strings;
using Roadnik.MAUI.Toolkit;
using Roadnik.MAUI.ViewModels;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Pages;

public partial class AboutPage : CContentPage
{
  private readonly AboutPageViewModel p_bindingCtx;
  private readonly IReadOnlyLifetime p_lifetime;
  private readonly IPreferencesStorage p_prefStorage;
  private readonly IHttpClientProvider p_httpClientProvider;
  private readonly ILogger p_log;
  private readonly string p_pkgPath = Path.Combine(FileSystem.Current.CacheDirectory, "update.apk");

  public AboutPage()
  {
    InitializeComponent();
    p_bindingCtx = (AboutPageViewModel)BindingContext;

    p_lifetime = Container.Locate<IReadOnlyLifetime>();
    p_prefStorage = Container.Locate<IPreferencesStorage>();
    p_httpClientProvider = Container.Locate<IHttpClientProvider>();
    p_log = Container.Locate<ILogger>()["about-page"];

    new FileInfo(p_pkgPath).TryDelete();

    p_lifetime.DisposeOnCompleted(Pool<EventLoopScheduler>.Get(out var checkUpdateScheduler));

    p_prefStorage.PreferencesChanged
      .Throttle(TimeSpan.FromSeconds(3), checkUpdateScheduler)
      .StartWithDefault()
      .ObserveOn(checkUpdateScheduler)
      .SelectAsync(async (_, _ct) =>
      {
        try
        {
          var serverAddress = p_prefStorage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
          if (string.IsNullOrWhiteSpace(serverAddress))
            return;

          var res = await p_httpClientProvider.Value.GetStringAsync($"{serverAddress.TrimEnd('/')}/check-github-update-apk", _ct);
          if (string.IsNullOrWhiteSpace(res))
            return;

          var updateInfo = JsonConvert.DeserializeObject<CheckUpdateRes>(res);
          if (updateInfo == null || !updateInfo.Success)
            return;

          p_bindingCtx.UpdateAvailable = updateInfo.Version > new SerializableVersion(AppInfo.Current.Version);
        }
        catch (Exception ex)
        {
          p_log.Error($"Error on update check", ex);
          p_bindingCtx.UpdateAvailable = false;
        }
      }, checkUpdateScheduler)
      .Subscribe(p_lifetime);
  }

  private async void UpdateAvailable_Clicked(object _sender, EventArgs _e)
  {
    p_bindingCtx.UpdateButtonText = $"{AppResources.page_about_updating} (0%)...";
    try
    {
      var serverAddress = p_prefStorage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
      if (string.IsNullOrWhiteSpace(serverAddress))
        return;

      var versionRes = await p_httpClientProvider.Value.GetStringAsync($"{serverAddress.TrimEnd('/')}/check-github-update-apk", p_lifetime.Token);
      if (string.IsNullOrWhiteSpace(versionRes))
        return;

      var updateInfo = JsonConvert.DeserializeObject<CheckUpdateRes>(versionRes);
      if (updateInfo == null || !updateInfo.Success)
        return;

      if (updateInfo.Version <= new SerializableVersion(AppInfo.Current.Version))
        return;

      var url = updateInfo.Url
        .Replace("{server-name}", $"{serverAddress.TrimEnd('/')}");

      using (var req = new HttpRequestMessage(HttpMethod.Get, url))
      using (var res = await p_httpClientProvider.Value.SendAsync(req, p_lifetime.Token))
      {
        res.EnsureSuccessStatusCode();
        using var remoteStream = await res.Content.ReadAsStreamAsync(p_lifetime.Token);
        if (res.Content.Headers.ContentLength == null)
        {
          p_bindingCtx.UpdateButtonText = $"{AppResources.page_about_updating} (progress unknown)...";
          using (var fileStream = File.Open(p_pkgPath, FileMode.Create))
            await remoteStream.CopyToAsync(fileStream, p_lifetime.Token);
        }
        else
        {
          void updateButtonText(double _progress)
          {
            p_bindingCtx.UpdateButtonText = $"{AppResources.page_about_updating} ({(int)_progress}%)...";
          }
          using (var remoteStreamWithProgress = new StreamWithProgress(res.Content.Headers.ContentLength.Value, remoteStream, updateButtonText))
          using (var fileStream = File.Open(p_pkgPath, FileMode.Create))
            await remoteStreamWithProgress.CopyToAsync(fileStream, p_lifetime.Token);
        }
      }

      await Launcher.OpenAsync(new OpenFileRequest($"Install version {updateInfo.Version}", new ReadOnlyFile(p_pkgPath)));
    }
    catch (Exception ex)
    {
      p_log.Error($"Error on installing update", ex);
    }
    finally
    {
      p_bindingCtx.UpdateButtonText = AppResources.page_about_updateAvailable;
      p_bindingCtx.UpdateAvailable = false;
    }

  }

}