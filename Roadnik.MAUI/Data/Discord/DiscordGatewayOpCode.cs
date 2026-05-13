namespace Roadnik.MAUI.Data.Discord;

internal enum DiscordGatewayOpCode
{
  Dispatch = 0,
  Heartbeat = 1,
  Identify = 2,
  PresenceUpdate = 3,
  Hello = 10,
  HeartbeatAck = 11,
}
