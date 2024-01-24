using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Toolkit;
using System.Reactive.Linq;

namespace Roadnik.MAUI.Platforms.Android.Services;

[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeLocation)]
public class BackgroundService : CAndroidService
{
  private readonly IReadOnlyLifetime p_globalLifetime;
  private readonly ILocationReporter p_locationReporter;
  private readonly NotificationManager p_notificationMgr;
  private ILifetime? p_lifetime;

  public BackgroundService()
  {
    p_globalLifetime = MauiProgram.Container.Locate<IReadOnlyLifetime>();
    p_locationReporter = MauiProgram.Container.Locate<ILocationReporter>();

    var context = global::Android.App.Application.Context;
    p_notificationMgr = (NotificationManager)context.GetSystemService(NotificationService)!;
  }

  public override IBinder OnBind(Intent? _intent)
  {
    throw new NotImplementedException();
  }

  [return: GeneratedEnum]
  public override StartCommandResult OnStartCommand(
    Intent? _intent,
    [GeneratedEnum] StartCommandFlags _flags,
    int _startId)
  {
    if (_intent?.Action == "START_SERVICE")
    {
      p_lifetime = p_globalLifetime.GetChildLifetime();
      if (p_lifetime == null)
        return StartCommandResult.NotSticky;

      var notification = GetRecordingNotification("Your location is being recorded...", "");
      if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
#pragma warning disable CA1416 // Validate platform compatibility
        StartForeground(Consts.NOTIFICATION_ID_RECORDING, notification, global::Android.Content.PM.ForegroundService.TypeLocation);
#pragma warning restore CA1416 // Validate platform compatibility
      else
        StartForeground(Consts.NOTIFICATION_ID_RECORDING, notification);

      p_locationReporter.Stats
        .Sample(TimeSpan.FromSeconds(1))
        .Subscribe(_ => GetRecordingNotification("Your location is being recorded...", $"Success: {_.Successful}; total: {_.Total}", true), p_lifetime);

      p_lifetime.DoOnEnding(() =>
      {
        StopForeground(StopForegroundFlags.Remove);
        StopSelfResult(_startId);
      });

      //Observable
      //  .Interval(TimeSpan.FromSeconds())

      return StartCommandResult.NotSticky;
    }
    else if (_intent?.Action == "STOP_SERVICE")
    {
      p_lifetime?.Dispose();
    }

    return StartCommandResult.NotSticky;
  }

  private Notification GetRecordingNotification(string _title, string _text, bool _notify = false)
  {
    var context = global::Android.App.Application.Context;
    var activity = PendingIntent.GetActivity(context, 0, Platform.CurrentActivity?.Intent, PendingIntentFlags.Immutable);

    var channelId = "ServiceChannel";
    var channel = new NotificationChannel(channelId, "Notify when recording is active", NotificationImportance.Max);
    p_notificationMgr.CreateNotificationChannel(channel);
    var builder = new Notification.Builder(this, channelId)
     .SetContentTitle(_title)
     .SetContentText(_text)
     .SetContentIntent(activity)
     .SetSmallIcon(Resource.Drawable.letter_r)
     .SetOnlyAlertOnce(true)
     .SetOngoing(true);

    if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
#pragma warning disable CA1416 // Validate platform compatibility
      builder = builder.SetForegroundServiceBehavior(1); // FOREGROUND_SERVICE_IMMEDIATE
#pragma warning restore CA1416 // Validate platform compatibility

    var notification = builder.Build();

    if (_notify)
      p_notificationMgr.Notify(Consts.NOTIFICATION_ID_RECORDING, notification);

    return notification;
  }

}
