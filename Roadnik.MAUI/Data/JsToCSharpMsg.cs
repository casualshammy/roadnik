using Newtonsoft.Json.Linq;

namespace Roadnik.MAUI.Data;

public record JsToCSharpMsg(string MsgType, JToken Data);