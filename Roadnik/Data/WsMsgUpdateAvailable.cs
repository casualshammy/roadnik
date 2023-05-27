using Roadnik.Attributes;

namespace Roadnik.Data;

[WebSocketMsg("ws-msg-data-updated")]
internal record WsMsgUpdateAvailable(long UnixTimeMs);
