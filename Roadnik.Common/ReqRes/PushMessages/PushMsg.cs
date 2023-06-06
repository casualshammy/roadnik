using Newtonsoft.Json.Linq;

namespace Roadnik.Common.ReqRes.PushMessages;

public record PushMsg(PushMsgType Type, JToken Data);
