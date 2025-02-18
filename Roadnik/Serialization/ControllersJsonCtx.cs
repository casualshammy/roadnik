using Ax.Fw;
using Roadnik.Common.ReqRes;
using Roadnik.Data;
using Roadnik.Server.Data.ReqRes;
using System.Text.Json.Serialization;

namespace Roadnik.Server.JsonCtx;

[JsonSourceGenerationOptions(
  PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(StorePathPointReq))]
[JsonSerializable(typeof(GetPathResData))]
[JsonSerializable(typeof(CreateNewPointReq))]
[JsonSerializable(typeof(IReadOnlyList<ListRoomPointsResData>))]
[JsonSerializable(typeof(DeleteRoomPointReq))]
[JsonSerializable(typeof(RoomInfo))]
[JsonSerializable(typeof(DeleteRoomReq))]
[JsonSerializable(typeof(IReadOnlyList<RoomInfo>))]
[JsonSerializable(typeof(SerializableVersion))]
internal partial class ControllersJsonCtx : JsonSerializerContext { }
