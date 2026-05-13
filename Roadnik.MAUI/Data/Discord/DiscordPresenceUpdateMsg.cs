using System.Text.Json.Serialization;

namespace Roadnik.MAUI.Data.Discord;

internal sealed record DiscordPresenceUpdateMsg(
  [property: JsonPropertyName("op")] DiscordGatewayOpCode Op,
  [property: JsonPropertyName("d")] DiscordPresenceUpdateData D);

internal sealed record DiscordPresenceUpdateData(
  [property: JsonPropertyName("since")] long? Since,
  [property: JsonPropertyName("activities")] DiscordActivity[] Activities,
  [property: JsonPropertyName("status")] string Status,
  [property: JsonPropertyName("afk")] bool Afk);

internal sealed record DiscordActivity(
  [property: JsonPropertyName("name")] string Name,
  [property: JsonPropertyName("type")] DiscordActivityType Type,
  [property: JsonPropertyName("details")] string? Details,
  [property: JsonPropertyName("state")] string? State,
  [property: JsonPropertyName("timestamps")] DiscordActivityTimestamps? Timestamps,
  [property: JsonPropertyName("application_id")] string? ApplicationId,
  [property: JsonPropertyName("buttons")] string[]? Buttons,
  [property: JsonPropertyName("metadata")] DiscordActivityMetadata? Metadata);

internal sealed record DiscordActivityTimestamps(
  [property: JsonPropertyName("start")] long Start);

internal sealed record DiscordActivityMetadata(
  [property: JsonPropertyName("button_urls")] string[] ButtonUrls);
