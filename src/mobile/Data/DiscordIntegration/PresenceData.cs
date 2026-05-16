namespace Roadnik.MAUI.Data.DiscordIntegration;

internal sealed record PresenceData(
  int SessionId,
  double Lat,
  double Lng,
  string RoomId,
  float? Speed,
  int? Hrm);
