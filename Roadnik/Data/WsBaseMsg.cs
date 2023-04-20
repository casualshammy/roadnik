using Newtonsoft.Json.Linq;

namespace Roadnik.Data;

public record WsBaseMsg(string Type, JToken Payload);
