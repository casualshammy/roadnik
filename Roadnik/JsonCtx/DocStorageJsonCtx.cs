using Roadnik.Common.Data;
using Roadnik.Common.Data.DocumentStorage;
using Roadnik.Server.Data.DbTypes;
using System.Text.Json.Serialization;

namespace Roadnik.Server.JsonCtx;

[JsonSerializable(typeof(StorageEntry))]
[JsonSerializable(typeof(RoomInfo))]
[JsonSerializable(typeof(RoomPointDocument))]
[JsonSerializable(typeof(RoomUserSession))]
internal partial class DocStorageJsonCtx : JsonSerializerContext { }
