using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roadnik.MAUI.Data.Discord;

internal sealed record DiscordGatewayMessage(
  [property: JsonPropertyName("op")] DiscordGatewayOpCode Op,
  [property: JsonPropertyName("d")] JsonElement? D,
  [property: JsonPropertyName("s")] int? S,
  [property: JsonPropertyName("t")] string? T);
