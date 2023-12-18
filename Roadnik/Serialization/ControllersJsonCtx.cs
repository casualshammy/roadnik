using Roadnik.Common.ReqRes;
using Roadnik.Data;
using Roadnik.Server.Data.ReqRes;
using System.Text.Json.Serialization;

namespace AxToolsServerNet.Data.Serializers;

[JsonSerializable(typeof(StorePathPointReq))]
[JsonSerializable(typeof(GetPathResData))]
[JsonSerializable(typeof(StartNewPathReq))]
[JsonSerializable(typeof(CreateNewPointReq))]
[JsonSerializable(typeof(IReadOnlyList<ListRoomPointsResData>))]
[JsonSerializable(typeof(DeleteRoomPointReq))]
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(DeleteRoomReq))]
[JsonSerializable(typeof(IReadOnlyList<User>))]
internal partial class ControllersJsonCtx : JsonSerializerContext
{

}
