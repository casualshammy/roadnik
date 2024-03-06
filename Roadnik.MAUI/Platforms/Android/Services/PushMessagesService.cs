using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using AndroidX.Core.App;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Firebase.Messaging;
using Roadnik.Common.ReqRes.PushMessages;
using Roadnik.Common.Serializers;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using System.Collections.Frozen;
using System.Text.Json;
using L = Roadnik.MAUI.Resources.Strings.AppResources;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Platforms.Android.Services;

[Service(DirectBootAware = true, Exported = true, Enabled = true)]
[IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
[IntentFilter(["com.google.firebase.INSTANCE_ID_EVENT"])]
public class PushMessagesService : FirebaseMessagingService
{
  private const string NOTIFICATION_CHANNEL_MAP_EVENTS = "NewPointAddedChannel";
  private const int REQUEST_POST_NOTIFICATIONS = 1000;
  private readonly FrozenDictionary<string, string> p_notificationsChannels;
  private readonly NotificationManager p_notificationManager;
  private bool p_firstShow = true;
  private int p_notificationId = 10000;

  public PushMessagesService()
  {
    p_notificationsChannels = new Dictionary<string, string>
    {
      { NOTIFICATION_CHANNEL_MAP_EVENTS, "Map events" },
    }.ToFrozenDictionary();

    var context = global::Android.App.Application.Context;
    p_notificationManager = (NotificationManager)context.GetSystemService(NotificationService)!;
  }

  public override void OnMessageReceived(RemoteMessage _message)
  {
    var data = _message.Data;

    try
    {
      if (data != null && data.TryGetValue("jsonData", out var jsonData) && !jsonData.IsNullOrEmpty())
      {
        Log.Info("roadnik", $"Received fcm message, data length: {data.Count}");

        if (Microsoft.Maui.Controls.Application.Current is not IMauiApp app)
        {
          Log.Error("roadnik", $"Can't get the instance of '{nameof(IMauiApp)}'!");
          return;
        }

        var prefStorage = app.Container.Locate<IPreferencesStorage>();
        var log = app.Container.Locate<ILog>()["fcm-service"];

        PushMsg? pushMsg = null;
        try
        {
          pushMsg = JsonSerializer.Deserialize(jsonData, AndroidPushJsonCtx.Default.PushMsg);
        }
        catch (Exception ex)
        {
          log.Error($"Error occured while trying to deserialize push msg data", ex);
          return;
        }

        if (pushMsg == null || pushMsg.Type == PushMsgType.None)
        {
          log.Error($"Can't parse push msg: it is null or its type is {nameof(PushMsgType.None)}!");
          return;
        }

        if (pushMsg.Type == PushMsgType.RoomPointAdded)
        {
          var msgData = pushMsg.Data.Deserialize(typeof(PushMsgRoomPointAdded), AndroidPushJsonCtx.Default) as PushMsgRoomPointAdded;
          if (msgData == null)
            return;

          var myUsername = prefStorage.GetValueOrDefault<string>(PREF_USERNAME);
          var enabled = prefStorage.GetValueOrDefault<bool>(PREF_NOTIFY_NEW_POINT);
          if (enabled == true && myUsername != msgData.Username)
          {
            var username = msgData.Username;
            if (username.IsNullOrWhiteSpace())
              username = "Unknown user";

            log.Info($"RoomPointAdded: '{username}' / '{msgData.Description}' / {msgData.Lat};{msgData.Lng}");

            var pushMsgData = new PushNotificationEvent(
              PUSH_MSG_NEW_POINT,
              JsonSerializer.SerializeToElement(new LatLng(msgData.Lat, msgData.Lng)));

            var title = L.notification_push_new_point_title
              .Replace("%username", username);
            var body = L.notification_push_new_point_body
              .Replace("%body", msgData.Description);

            ShowNotificationGoToLocation(
              NOTIFICATION_CHANNEL_MAP_EVENTS,
              title,
              $"\"{body}\"",
              pushMsgData);
          }
        }
        else if (pushMsg.Type == PushMsgType.NewTrackStarted)
        {
          var msgData = pushMsg.Data.Deserialize(typeof(PushMsgNewTrackStarted), AndroidPushJsonCtx.Default) as PushMsgNewTrackStarted;
          if (msgData == null)
            return;

          var myUsername = prefStorage.GetValueOrDefault<string>(PREF_USERNAME);
          var enabled = prefStorage.GetValueOrDefault<bool>(PREF_NOTIFY_NEW_TRACK);
          if (enabled == true && myUsername != msgData.Username)
          {
            log.Info($"NewTrackStarted: '{msgData.Username}'");

            var pushMsgData = new PushNotificationEvent(
              PUSH_MSG_NEW_TRACK,
              JsonSerializer.SerializeToElement(msgData.Username));

            var title = L.notification_push_new_track_title
              .Replace("%username", msgData.Username);
            var body = L.notification_push_new_track_body;

            ShowNotificationGoToLocation(
              NOTIFICATION_CHANNEL_MAP_EVENTS,
              title,
              body,
              pushMsgData);
          }
        }
      }
    }
    catch (Exception ex)
    {
      Log.Error("roadnik", $"Can't process push message: {ex}");
    }
  }

  private void ShowNotificationGoToLocation(
    string _notificationChannel,
    string _contentTitle,
    string _contentText,
    PushNotificationEvent _data)
  {
    try
    {
      if (p_firstShow && Build.VERSION.SdkInt > BuildVersionCodes.SV2 && Platform.CurrentActivity != null)
        ActivityCompat.RequestPermissions(Platform.CurrentActivity, ["android.permission.POST_NOTIFICATIONS"], REQUEST_POST_NOTIFICATIONS);

      var channel = new NotificationChannel(_notificationChannel, p_notificationsChannels[_notificationChannel], NotificationImportance.Max);
      channel.SetShowBadge(true);
      p_notificationManager.CreateNotificationChannel(channel);

      var context = global::Android.App.Application.Context;
      var openAppIntent = new Intent(context, typeof(MainActivity));
      var openAppIntentExtra = JsonSerializer.Serialize(_data);
      openAppIntent.PutExtra("push-msg", openAppIntentExtra);
      var openAppPendingIntent = PendingIntent.GetActivity(context, 0, openAppIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

      var builder = new Notification.Builder(this, _notificationChannel)
       .SetContentIntent(openAppPendingIntent)
       .SetSmallIcon(Resource.Drawable.letter_r_blue)
       .SetContentTitle(_contentTitle)
       .SetContentText(_contentText)
       .SetAutoCancel(true);

      var notification = builder.Build();

      p_notificationManager.Notify(Interlocked.Increment(ref p_notificationId), notification);
    }
    finally
    {
      p_firstShow = false;
    }

  }

}
