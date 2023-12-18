using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Toolkit;
using System.Reactive.Linq;

namespace Roadnik.MAUI.Platforms.Android.Services;

[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeLocation)]
public class LocationReporterService : CAndroidService
{
  private const int NOTIFICATION_ID = 100;
  private const int REQUEST_POST_NOTIFICATIONS = 1000;
  private readonly IReadOnlyLifetime p_globalLifetime;
  private readonly ILocationReporter p_locationReporter;
  private ILifetime? p_lifetime;

  public LocationReporterService()
  {
    p_globalLifetime = MauiProgram.Container.Locate<IReadOnlyLifetime>();
    p_locationReporter = MauiProgram.Container.Locate<ILocationReporter>();
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

      RegisterNotification();
      p_locationReporter.SetState(true);

      p_locationReporter.Stats
        .Sample(TimeSpan.FromSeconds(1))
        .Subscribe(_ => GetNotification("Your location is being recorded...", $"Success: {_.Successful}; total: {_.Total}", true), p_lifetime);

      p_lifetime.DoOnEnding(() =>
      {
        StopForeground(StopForegroundFlags.Remove);
        StopSelfResult(_startId);
        p_locationReporter.SetState(false);
      });

      return StartCommandResult.NotSticky;
    }
    else if (_intent?.Action == "STOP_SERVICE")
    {
      p_lifetime?.Dispose();
    }

    return StartCommandResult.NotSticky;
  }

  public void Start()
  {
    var context = global::Android.App.Application.Context;
    var intent = new Intent(context, typeof(LocationReporterService));
    intent.SetAction("START_SERVICE");
    context.StartForegroundService(intent);
  }

  public void Stop()
  {
    var context = global::Android.App.Application.Context;
    var intent = new Intent(context, Class);
    intent.SetAction("STOP_SERVICE");
    context.StartService(intent);
  }

  private void RegisterNotification()
  {
    var notification = GetNotification("Your location is being recorded...", "");
    if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
#pragma warning disable CA1416 // Validate platform compatibility
      StartForeground(NOTIFICATION_ID, notification, global::Android.Content.PM.ForegroundService.TypeLocation);
#pragma warning restore CA1416 // Validate platform compatibility
    else
      StartForeground(NOTIFICATION_ID, notification);
  }

  private Notification GetNotification(string _title, string _text, bool _notify = false)
  {
    var context = global::Android.App.Application.Context;
    var manager = (NotificationManager)context.GetSystemService(NotificationService)!;
    var activity = PendingIntent.GetActivity(context, 0, Platform.CurrentActivity?.Intent, PendingIntentFlags.Immutable);

    if (Build.VERSION.SdkInt > BuildVersionCodes.SV2 && Platform.CurrentActivity != null)
    {
      if (ActivityCompat.ShouldShowRequestPermissionRationale(Platform.CurrentActivity, "android.permission.POST_NOTIFICATIONS"))
      {
        ActivityCompat.RequestPermissions(Platform.CurrentActivity, new[] { "android.permission.POST_NOTIFICATIONS" }, REQUEST_POST_NOTIFICATIONS);
      }
    }

    var channelId = "ServiceChannel";
    var channel = new NotificationChannel(channelId, "Notify when recording is active", NotificationImportance.Max);
    manager.CreateNotificationChannel(channel);
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
      manager.Notify(NOTIFICATION_ID, notification);

    return notification;
  }

}
