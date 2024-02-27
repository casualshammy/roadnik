using Roadnik.Data;
using System.Text.Json.Serialization;

namespace AxToolsServerNet.Data.Serializers;

[JsonSerializable(typeof(StorageEntry))]
[JsonSerializable(typeof(RoomInfo))]
[JsonSerializable(typeof(GeoPointEntry))]
internal partial class DocStorageJsonCtx : JsonSerializerContext
{

}