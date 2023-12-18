using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using Ax.Fw.Attributes;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Grace.DependencyInjection.Attributes;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Toolkit;
using System.Reactive.Linq;

namespace Roadnik.MAUI.Platforms.Android.Services;

[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeLocation)]
[ExportClass(typeof(ILocationReporterService), Singleton: true)]
public class LocationReporterService : CAndroidService, ILocationReporterService
{
  private const int NOTIFICATION_ID = 100;
  private const int REQUEST_POST_NOTIFICATIONS = 1000;
  private ILifetime? p_lifetime;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

  [Import]
  public ILocationReporter LocationReporter { get; init; }

  [Import]
  public IReadOnlyLifetime Lifetime { get; init; }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

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
      p_lifetime = Lifetime.GetChildLifetime();
      if (p_lifetime == null)
        return StartCommandResult.NotSticky;

      RegisterNotification();
      LocationReporter.SetState(true);

      LocationReporter.Stats
        .Take(1)
        .Concat(LocationReporter.Stats.Sample(TimeSpan.FromSeconds(1)))
        .Subscribe(_ => GetNotification("Your location is being recorded...", $"Success: {_.Successful}; total: {_.Total}", true), p_lifetime);

      p_lifetime.DoOnEnding(() =>
      {
        StopForeground(StopForegroundFlags.Remove);
        StopSelfResult(_startId);
        LocationReporter.SetState(false);
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
    if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
#pragma warning disable CA1416 // Validate platform compatibility
      context.StartForegroundService(intent);
#pragma warning restore CA1416 // Validate platform compatibility
    else
      context.StartService(intent);
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

    Notification notification;
    if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
    {
      var channelId = "ServiceChannel";

#pragma warning disable CA1416 // Validate platform compatibility

      var channel = new NotificationChannel(channelId, "Notify when recording is active", NotificationImportance.Max);
      manager.CreateNotificationChannel(channel);
      var builder = new Notification.Builder(this, channelId)
       .SetContentTitle(_title)
       .SetContentText(_text)
       .SetContentIntent(activity)
       .SetSmallIcon(Resource.Drawable.letter_r)
       .SetOnlyAlertOnce(true)
       .SetOngoing(true);

#pragma warning restore CA1416 // Validate platform compatibility

      if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
#pragma warning disable CA1416 // Validate platform compatibility
        builder = builder.SetForegroundServiceBehavior(1); // FOREGROUND_SERVICE_IMMEDIATE
#pragma warning restore CA1416 // Validate platform compatibility

      notification = builder.Build();
    }
    else
    {
#pragma warning disable CS0618 // Type or member is obsolete

      var builder = new NotificationCompat.Builder(this)
        .SetContentTitle(_title)
        .SetContentText(_text)
        .SetContentIntent(activity)
        .SetSmallIcon(Resource.Drawable.letter_r)
        .SetOnlyAlertOnce(true)
        .SetOngoing(true);

#pragma warning restore CS0618 // Type or member is obsolete

      notification = builder.Build();
    }

    if (_notify)
      manager.Notify(NOTIFICATION_ID, notification);

    return notification;
  }

}
