using Roadnik.Attributes;

namespace Roadnik.Data;

[WebSocketMsg("ws-msg-path-wiped")]
internal record WsMsgPathWiped(string Username);