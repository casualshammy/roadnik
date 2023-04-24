using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Ax.Fw;
using Ax.Fw.Attributes;
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
  [Import]
  public ILocationReporter LocationReporter { get; init; }

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
      RegisterNotification();
      LocationReporter.SetState(true);
    }
    else if (_intent?.Action == "STOP_SERVICE")
    {
      StopForeground(true);
      StopSelfResult(_startId);
      LocationReporter.SetState(false);
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
    var context = global::Android.App.Application.Context;
    var channel = new NotificationChannel("ServiceChannel", "LocationReporterService", NotificationImportance.Max);
    var manager = (NotificationManager)context.GetSystemService(NotificationService)!;
    manager.CreateNotificationChannel(channel);
    var notification = new Notification.Builder(this, "ServiceChannel")
       .SetContentTitle("Roadnik")
       .SetContentText("Your location being recorded...")
       .SetSmallIcon(Resource.Drawable.letter_r)
       .SetOngoing(true)
       .Build();

    StartForeground(100, notification);
  }

}
