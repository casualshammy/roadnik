using Roadnik.MAUI.Data.Discord;
using Roadnik.MAUI.Data.Nominatim;
using System.Text.Json.Serialization;

namespace Roadnik.MAUI.JsonCtx;

[JsonSerializable(typeof(DiscordTokenData))]
[JsonSerializable(typeof(DiscordGatewayMessage))]
[JsonSerializable(typeof(DiscordHelloData))]
[JsonSerializable(typeof(DiscordUserResponse))]
[JsonSerializable(typeof(DiscordHeartbeatMsg))]
[JsonSerializable(typeof(DiscordIdentifyMsg))]
[JsonSerializable(typeof(DiscordPresenceUpdateMsg))]
[JsonSerializable(typeof(NominatimReverseResponse))]
internal partial class DiscordJsonCtx : JsonSerializerContext { }
