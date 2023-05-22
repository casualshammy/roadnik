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

  public AboutPage()
  {
    InitializeComponent();
    p_bindingCtx = (AboutPageViewModel)BindingContext;

    p_lifetime = Container.Locate<IReadOnlyLifetime>();
    p_prefStorage = Container.Locate<IPreferencesStorage>();
    p_httpClientProvider = Container.Locate<IHttpClientProvider>();
    p_log = Container.Locate<ILogger>()["about-page"];

    p_lifetime.ToDisposeOnEnded(Pool<EventLoopScheduler>.Get(out var checkUpdateScheduler));

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

          var res = await p_httpClientProvider.Value.GetStringAsync($"{serverAddress.TrimEnd('/')}/{ReqPaths.CHECK_UPDATE_APK}", _ct);
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
    try
    {
      var serverAddress = p_prefStorage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
      if (string.IsNullOrWhiteSpace(serverAddress))
        return;

      var versionRes = await p_httpClientProvider.Value.GetStringAsync($"{serverAddress.TrimEnd('/')}/{ReqPaths.CHECK_UPDATE_APK}", p_lifetime.Token);
      if (string.IsNullOrWhiteSpace(versionRes))
        return;

      var updateInfo = JsonConvert.DeserializeObject<CheckUpdateRes>(versionRes);
      if (updateInfo == null || !updateInfo.Success)
        return;

      if (updateInfo.Version <= new SerializableVersion(AppInfo.Current.Version))
        return;

      var url = updateInfo.Url
        .Replace("{server-name}", $"{serverAddress.TrimEnd('/')}");

      await Launcher.Default.OpenAsync(url);
    }
    catch (Exception ex)
    {
      p_log.Error($"Error on getting update url", ex);
    }
    finally
    {
      p_bindingCtx.UpdateButtonText = AppResources.page_about_updateAvailable;
      p_bindingCtx.UpdateAvailable = false;
    }

  }

}