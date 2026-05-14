using System.Text.Json.Serialization;

namespace Roadnik.MAUI.Data.Discord;

internal sealed record DiscordIdentifyMsg(
  [property: JsonPropertyName("op")] DiscordGatewayOpCode Op,
  [property: JsonPropertyName("d")] DiscordIdentifyData D);

internal sealed record DiscordIdentifyData(
  [property: JsonPropertyName("token")] string Token,
  [property: JsonPropertyName("capabilities")] int Capabilities,
  [property: JsonPropertyName("compress")] bool Compress,
  [property: JsonPropertyName("properties")] DiscordIdentifyProperties Properties);

internal sealed record DiscordIdentifyProperties(
  [property: JsonPropertyName("os")] string Os,
  [property: JsonPropertyName("browser")] string Browser,
  [property: JsonPropertyName("device")] string Device);
