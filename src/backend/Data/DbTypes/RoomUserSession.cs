using Ax.Fw.SharedTypes.Attributes;

namespace Roadnik.Server.Data.DbTypes;

[SimpleDocument("room-user-session")]
internal record RoomUserSession(
  int SessionId);
