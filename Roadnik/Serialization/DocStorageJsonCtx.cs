using Roadnik.Data;
using Roadnik.Server.Data;
using Roadnik.Server.Data.DbTypes;
using System.Text.Json.Serialization;

namespace Roadnik.Server.JsonCtx;

[JsonSerializable(typeof(StorageEntry))]
[JsonSerializable(typeof(RoomInfo))]
[JsonSerializable(typeof(GeoPointEntry))]
[JsonSerializable(typeof(RoomUserSession))]
internal partial class DocStorageJsonCtx : JsonSerializerContext { }
