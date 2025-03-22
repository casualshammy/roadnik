﻿using Ax.Fw;
using Roadnik.Common.Data;
using Roadnik.Common.ReqRes;
using System.Text.Json.Serialization;

namespace Roadnik.Common.JsonCtx;

[JsonSourceGenerationOptions(
  PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(StorePathPointReq))]
[JsonSerializable(typeof(StorePathPointRes))]
[JsonSerializable(typeof(GetPathResData))]
[JsonSerializable(typeof(CreateNewPointReq))]
[JsonSerializable(typeof(ListRoomPointsRes))]
[JsonSerializable(typeof(DeleteRoomPointReq))]
[JsonSerializable(typeof(RoomInfo))]
[JsonSerializable(typeof(DeleteRoomReq))]
[JsonSerializable(typeof(IReadOnlyList<RoomInfo>))]
[JsonSerializable(typeof(SerializableVersion))]
public partial class RestJsonCtx : JsonSerializerContext { }
