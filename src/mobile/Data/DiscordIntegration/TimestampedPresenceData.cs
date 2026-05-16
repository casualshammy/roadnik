namespace Roadnik.MAUI.Data.DiscordIntegration;

internal sealed record TimestampedPresenceData(
  PresenceData? Data,
  DateTimeOffset Timestamp)
{
  public static TimestampedPresenceData Default { get; } = new(null, default);
}
