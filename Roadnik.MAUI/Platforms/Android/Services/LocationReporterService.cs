using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Ax.Fw;
using Ax.Fw.Attributes;
using Ax.Fw.SharedTypes.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI.Platforms.Android.Services;

[Service]
[ExportClass(typeof(IAndroidService), Singleton: true)]
public class LocationReporterService : Service, IAndroidService
{
  private ILifetime? p_serviceLifetime;

  public LocationReporterService()
  {

  }

  public override IBinder OnBind(Intent? _intent)
  {
    throw new NotImplementedException();
  }

  [return: GeneratedEnum]//we catch the actions intents to know the state of the foreground service
  public override StartCommandResult OnStartCommand(
    Intent? _intent, 
    [GeneratedEnum] StartCommandFlags _flags, 
    int _startId)
  {
    if (_intent?.Action == "START_SERVICE")
    {
      RegisterNotification();

      p_serviceLifetime = new Lifetime();
      if (p_serviceLifetime == null)
        return StartCommandResult.NotSticky;


    }
    else if (_intent?.Action == "STOP_SERVICE")
    {
      p_serviceLifetime?.Complete();
      StopForeground(true);
      StopSelfResult(_startId);
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
