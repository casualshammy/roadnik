using Android.App;
using Android.Util;
using Ax.Fw.Extensions;
using Firebase.Messaging;
using JustLogger.Interfaces;
using Newtonsoft.Json.Linq;
using Roadnik.Common.ReqRes.PushMessages;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Platforms.Android.Services;

[Service(DirectBootAware = true, Exported = true, Enabled = true)]
[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
[IntentFilter(new[] { "com.google.firebase.INSTANCE_ID_EVENT" })]
public class FCMService : FirebaseMessagingService
{
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

      var notificationMgr = app.Container.Locate<INotificationMgr>();
      var prefStorage = app.Container.Locate<IPreferencesStorage>();
      var log = app.Container.Locate<ILogger>()["fcm-service"];

      var msg = JObject.Parse(jsonData).ToObject<PushMsg>();
      if (msg == null || msg.Type == PushMsgType.None)
      {
        log.Error($"Can't parse push msg: it is null or its type is {nameof(PushMsgType.None)}!");
        return;
      }

      if (msg.Type == PushMsgType.RoomPointAdded)
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
          notificationMgr.ShowNotification(
            NOTIFICATION_NEW_POINT, 
            $"User '{username}' has added new point to map", 
            $"\"{msgData.Description}\"", 
            NOTIFICATION_CHANNEL_MAP_EVENTS,
            JToken.FromObject(new LatLng(msgData.Lat, msgData.Lng)));
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
          notificationMgr.ShowNotification(
            NOTIFICATION_NEW_TRACK,
            $"User '{msgData.Username}' has started a new track",
            "Click to open map",
            NOTIFICATION_CHANNEL_MAP_EVENTS,
            JToken.FromObject(msgData.Username));
        }
      }
    }
  }

}
