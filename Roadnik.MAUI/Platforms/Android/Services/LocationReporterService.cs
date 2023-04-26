using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Webkit;
using Ax.Fw;
using Ax.Fw.Attributes;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Grace.DependencyInjection.Attributes;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Toolkit;
using System.Reactive.Linq;

namespace Roadnik.MAUI.Platforms.Android.Services;

[Service]
[ExportClass(typeof(ILocationReporterService), Singleton: true)]
public class LocationReporterService : CAndroidService, ILocationReporterService
{
  private const int NOTIFICATION_ID = 100;
  private ILifetime? p_lifetime;

  [Import]
  public ILocationReporter LocationReporter { get; init; }

  [Import]
  public IReadOnlyLifetime Lifetime { get; init; }

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
        .Sample(TimeSpan.FromSeconds(1))
        .Subscribe(_ => GetNotification($"Your location is being recorded ({_.Successful}/{_.Total})...", true), p_lifetime);

      p_lifetime.DoOnCompleted(() =>
      {
        StopForeground(true);
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
      context.StartForegroundService(intent);
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
    var notification = GetNotification("Your location is being recorded...");
    StartForeground(NOTIFICATION_ID, notification);
  }

  private Notification GetNotification(string _text, bool _notify = false)
  {
    var context = global::Android.App.Application.Context;
    var channel = new NotificationChannel("ServiceChannel", "Notify when recording is active", NotificationImportance.Max);
    var manager = (NotificationManager)context.GetSystemService(NotificationService)!;
    manager.CreateNotificationChannel(channel);

    var activity = PendingIntent.GetActivity(context, 0, Platform.CurrentActivity?.Intent, 0);

    var builder = new Notification.Builder(this, channel.Id)
       .SetContentTitle("Roadnik")
       .SetContentText(_text)
       .SetContentIntent(activity)
       .SetSmallIcon(Resource.Drawable.letter_r)
       .SetOnlyAlertOnce(true)
       .SetOngoing(true);

#pragma warning disable CA1416 // Validate platform compatibility
    if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
      builder = builder.SetForegroundServiceBehavior(1); // FOREGROUND_SERVICE_IMMEDIATE
#pragma warning restore CA1416 // Validate platform compatibility

    var notification = builder.Build();

    if (_notify)
      manager.Notify(NOTIFICATION_ID, notification);

    return notification;
  }

}
