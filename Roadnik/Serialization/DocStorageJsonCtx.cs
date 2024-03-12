using Roadnik.Data;
using System.Text.Json.Serialization;

namespace Roadnik.Server.JsonCtx;

[JsonSerializable(typeof(StorageEntry))]
[JsonSerializable(typeof(RoomInfo))]
[JsonSerializable(typeof(GeoPointEntry))]
internal partial class DocStorageJsonCtx : JsonSerializerContext
{

}