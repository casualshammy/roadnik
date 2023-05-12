#if ANDROID
using Android.App;
using Android.OS;
using AndroidX.Core.App;
#endif
namespace Roadnik.MAUI.Toolkit;

internal static class SimpleNotification
{
  public static void Show(
    int _notificationId, 
    string _channelId, 
    string _channelName, 
    string _title, 
    string _message)
  {
#if ANDROID
    var context = global::Android.App.Application.Context;
    var manager = (NotificationManager)context.GetSystemService(global::Android.App.Application.NotificationService)!;
    var activity = PendingIntent.GetActivity(context, 0, Platform.CurrentActivity?.Intent, 0);

    Notification notification;
    if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
    {
#pragma warning disable CA1416 // Validate platform compatibility
      var channel = new NotificationChannel(_channelId, _channelName, NotificationImportance.Default);
      manager.CreateNotificationChannel(channel);
      var builder = new Notification.Builder(context, _channelId)
       .SetContentTitle(_title)
       .SetContentText(_message)
       .SetContentIntent(activity)
       .SetSmallIcon(Resource.Drawable.letter_r);
#pragma warning restore CA1416 // Validate platform compatibility

      notification = builder.Build();
    }
    else
    {
#pragma warning disable CS0618 // Type or member is obsolete

      var builder = new NotificationCompat.Builder(context)
        .SetContentTitle(_title)
        .SetContentText(_message)
        .SetContentIntent(activity)
        .SetSmallIcon(Resource.Drawable.letter_r);

#pragma warning restore CS0618 // Type or member is obsolete

      notification = builder.Build();
    }

    manager.Notify(_notificationId, notification);

#endif
  }


}
