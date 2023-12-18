using System.Text.Json;

namespace Roadnik.Common.ReqRes.PushMessages;

public record PushMsg(PushMsgType Type, JsonElement Data);
