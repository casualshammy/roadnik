using Android.App;
using Android.OS;
using Android.Util;
using AndroidX.Core.App;
using Ax.Fw.Extensions;
using Firebase.Messaging;
using JustLogger.Interfaces;
using Newtonsoft.Json.Linq;
using Roadnik.Common.ReqRes.PushMessages;
using Roadnik.MAUI.Interfaces;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Platforms.Android.Services;

[Service(DirectBootAware = true, Exported = true, Enabled = true)]
[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
[IntentFilter(new[] { "com.google.firebase.INSTANCE_ID_EVENT" })]
public class FCMService : FirebaseMessagingService
{
  private const int REQUEST_POST_NOTIFICATIONS = 1000;
  private int p_notificationId = 1000;

  public override void OnMessageReceived(RemoteMessage _message)
  {
    var data = _message.Data;
    if (data != null && data.TryGetValue("jsonData", out var jsonData) && !jsonData.IsNullOrEmpty())
    {
      Log.Info("roadnik", $"Received fcm message, data length: {data.Count}");

      if (Microsoft.Maui.Controls.Application.Current is not IMauiApp app)
      {
        Log.Error("roadnik", $"Can't get the instance of '{nameof(IMauiApp)}'!");
        return;
      }

      var prefStorage = app.Container.Locate<IPreferencesStorage>();
      var log = app.Container.Locate<ILogger>()["fcm-service"];

      var msg = JObject.Parse(jsonData).ToObject<PushMsg>();
      if (msg == null || msg.Type == PushMsgType.None)
      {
        log.Error($"Can't parse push msg: it is null or its type is {nameof(PushMsgType.None)}!");
        return;
      }

      if (msg.Type == PushMsgType.Notification)
      {
        var notificationData = msg.Data.ToObject<PushMsgNotification>();
        if (notificationData == null)
          return;

        log.Info($"Notification: '{notificationData.Title}' / '{notificationData.Text}'");
        ShowNotification(notificationData.Title, notificationData.Text);
      }
      else if (msg.Type == PushMsgType.RoomPointAdded)
      {
        var msgData = msg.Data.ToObject<PushMsgRoomPointAdded>();
        if (msgData == null)
          return;

        var myUsername = prefStorage.GetValueOrDefault<string>(PREF_USERNAME);
        var enabled = prefStorage.GetValueOrDefault<bool>(PREF_NOTIFY_NEW_POINT);
        if (enabled == true && myUsername != msgData.Username)
        {
          var username = msgData.Username;
          if (username.IsNullOrWhiteSpace())
            username = "Unknown user";

          log.Info($"RoomPointAdded: '{username}' / '{msgData.Description}'");
          ShowNotification($"User '{username}' has added new point to map", $"\"{msgData.Description}\"");
        }
      }
      else if (msg.Type == PushMsgType.NewTrackStarted)
      {
        var msgData = msg.Data.ToObject<PushMsgNewTrackStarted>();
        if (msgData == null)
          return;

        var myUsername = prefStorage.GetValueOrDefault<string>(PREF_USERNAME);
        var enabled = prefStorage.GetValueOrDefault<bool>(PREF_NOTIFY_NEW_TRACK);
        if (enabled == true && myUsername != msgData.Username)
        {
          log.Info($"NewTrackStarted: '{msgData.Username}'");
          ShowNotification($"User '{msgData.Username}' has started a new track", "Click to open map");
        }
      }
    }
  }

  private void ShowNotification(string _title, string? _msg)
  {
    var context = global::Android.App.Application.Context;
    var manager = (NotificationManager)context.GetSystemService(NotificationService)!;
    var activityIntent = Platform.CurrentActivity?.Intent ?? new global::Android.Content.Intent(this, typeof(MainActivity));
    var activity = PendingIntent.GetActivity(context, 0, activityIntent, PendingIntentFlags.Immutable);

    if (Build.VERSION.SdkInt > BuildVersionCodes.SV2 && Platform.CurrentActivity != null)
      if (ActivityCompat.ShouldShowRequestPermissionRationale(Platform.CurrentActivity, "android.permission.POST_NOTIFICATIONS"))
        ActivityCompat.RequestPermissions(Platform.CurrentActivity, new[] { "android.permission.POST_NOTIFICATIONS" }, REQUEST_POST_NOTIFICATIONS);

    global::Android.App.Notification notification;
    if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
    {
      var channelId = "EventsChannel";

#pragma warning disable CA1416 // Validate platform compatibility

      var channel = new NotificationChannel(channelId, "Various event notifications", NotificationImportance.Max);
      manager.CreateNotificationChannel(channel);
      var builder = new global::Android.App.Notification.Builder(this, channelId)
       .SetContentTitle(_title)
       .SetContentText(_msg)
       .SetContentIntent(activity)
       .SetSmallIcon(Resource.Drawable.letter_r)
       .SetAutoCancel(true);

#pragma warning restore CA1416 // Validate platform compatibility

      notification = builder.Build();
    }
    else
    {
#pragma warning disable CS0618 // Type or member is obsolete

      var builder = new NotificationCompat.Builder(this)
        .SetPriority(NotificationCompat.PriorityMax)
        .SetContentTitle(_title)
        .SetContentText(_msg)
        .SetContentIntent(activity)
        .SetSmallIcon(Resource.Drawable.letter_r)
        .SetAutoCancel(true);

#pragma warning restore CS0618 // Type or member is obsolete

      notification = builder.Build();
    }

    manager.Notify(Interlocked.Increment(ref p_notificationId), notification);
  }

}
