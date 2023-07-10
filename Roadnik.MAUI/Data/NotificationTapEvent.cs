using Newtonsoft.Json.Linq;

namespace Roadnik.MAUI.Data;

internal record NotificationTapEvent(long Timestamp, int NotificationId, bool IsDismissed, JToken? Data);
