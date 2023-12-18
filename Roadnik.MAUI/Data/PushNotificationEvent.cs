using System.Text.Json;

namespace Roadnik.MAUI.Data;

//internal record NotificationTapEvent(long Timestamp, int NotificationId, bool IsDismissed, JToken? Data);

internal record PushNotificationEvent(int NotificationId, JsonElement Data);