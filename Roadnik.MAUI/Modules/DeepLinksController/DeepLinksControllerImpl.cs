using Ax.Fw.Attributes;
using CommunityToolkit.Maui.Alerts;
using Roadnik.MAUI.Interfaces;
using System.Text.RegularExpressions;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Modules.DeepLinksController;

[ExportClass(typeof(IDeepLinksController), Singleton: true)]
internal class DeepLinksControllerImpl : IDeepLinksController
{
  public const string AndroidExtraKey = "open-link";
  private static readonly Regex p_urlRegex = new(@"^(.+?)/\?roomId=([\w\-_]+)$");

  private readonly IPagesController p_pagesController;
  private readonly IPreferencesStorage p_preferencesStorage;
  private volatile bool p_mainPageStarted = false;

  public DeepLinksControllerImpl(
    IPagesController _pagesController,
    IPreferencesStorage _preferencesStorage)
  {
    p_pagesController = _pagesController;
    p_preferencesStorage = _preferencesStorage;
  }

  public async Task NewDeepLinkAsync(string _url, CancellationToken _ct = default)
  {
    var match = p_urlRegex.Match(_url);
    if (!match.Success)
    {
      await Toast
        .Make("Incorrect room id", CommunityToolkit.Maui.Core.ToastDuration.Long)
        .Show(_ct);

      return;
    }

    if (p_pagesController.CurrentPage == null && !p_mainPageStarted)
    {
      p_mainPageStarted = true;
#if ANDROID
      var intent = new Android.Content.Intent(Android.App.Application.Context, typeof(MainActivity));
      intent.SetFlags(Android.Content.ActivityFlags.ClearTask | Android.Content.ActivityFlags.NewTask);
      intent.PutExtra(AndroidExtraKey, _url);
      Android.App.Application.Context.StartActivity(intent);
#endif
      return;
    }

    using var timedCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(_ct, timedCts.Token);

    while (!cts.IsCancellationRequested && p_pagesController.CurrentPage == null)
      await Task.Delay(100, cts.Token);

    if (p_pagesController.CurrentPage == null)
      return;

    var oldServerAddress = p_preferencesStorage.GetValueOrDefault<string>(PREF_SERVER_ADDRESS);
    var oldRoomId = p_preferencesStorage.GetValueOrDefault<string>(PREF_ROOM);

    var newServerAddress = match.Groups[1].Value;
    var newRoomId = match.Groups[2].Value;

    if (oldServerAddress == newServerAddress && oldRoomId == newRoomId)
    {
      await Toast
        .Make("Already using this server/room", CommunityToolkit.Maui.Core.ToastDuration.Long)
        .Show(_ct);

      return;
    }

    var result = await p_pagesController.CurrentPage.Dispatcher.DispatchAsync(() => p_pagesController.CurrentPage.DisplayAlert(
      $"Do you want to switch server/room?",
      $"New address: '{newServerAddress}'.\nNew room id: '{newRoomId}'\nYour previous address will be not saved.",
      "Yes",
      "Cancel"));

    if (result)
    {
      p_preferencesStorage.SetValue(PREF_SERVER_ADDRESS, newServerAddress);
      p_preferencesStorage.SetValue(PREF_ROOM, newRoomId);

      await Toast
        .Make("Saved")
        .Show(_ct);
    }
  }

}

