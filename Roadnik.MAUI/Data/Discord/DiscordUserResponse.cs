using System.Text.Json.Serialization;

namespace Roadnik.MAUI.Data.Discord;

internal sealed record DiscordUserResponse(
  [property: JsonPropertyName("username")] string? Username);
