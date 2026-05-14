using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace Roadnik.Server.JsonCtx;

[JsonSerializable(typeof(ProblemDetails))]
internal partial class AdditionalRestJsonCtx : JsonSerializerContext { }
