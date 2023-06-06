using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Roadnik.Common.ReqRes.PushMessages;

[JsonConverter(typeof(StringEnumConverter))]
public enum PushMsgType
{
  None = 0,
  Notification,
  RoomPointAdded,
  NewTrackStarted
}
