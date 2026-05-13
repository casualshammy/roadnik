using System.Text.Json.Serialization;

namespace Roadnik.MAUI.Data.Discord;

internal sealed record DiscordHeartbeatMsg(
  [property: JsonPropertyName("op")] DiscordGatewayOpCode Op,
  [property: JsonPropertyName("d")] int? D);
