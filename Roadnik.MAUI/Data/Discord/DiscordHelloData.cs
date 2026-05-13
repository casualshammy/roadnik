using System.Text.Json.Serialization;

namespace Roadnik.MAUI.Data.Discord;

internal sealed record DiscordHelloData(
  [property: JsonPropertyName("heartbeat_interval")] int HeartbeatInterval);
