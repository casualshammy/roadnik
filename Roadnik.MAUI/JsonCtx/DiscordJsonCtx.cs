using Roadnik.MAUI.Data.Discord;
using System.Text.Json.Serialization;

namespace Roadnik.MAUI.JsonCtx;

[JsonSerializable(typeof(DiscordTokenData))]
internal partial class DiscordJsonCtx : JsonSerializerContext { }
