using Roadnik.Attributes;

namespace Roadnik.Data;

[WebSocketMsg("ws-msg-hello")]
internal record WsMsgHello(long UnixTimeMs);
