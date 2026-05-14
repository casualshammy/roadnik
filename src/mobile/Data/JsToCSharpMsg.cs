using System.Text.Json;

namespace Roadnik.MAUI.Data;

public record JsToCSharpMsg(string MsgType, JsonElement Data);