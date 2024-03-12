using Roadnik.Modules.FCMProvider;
using Roadnik.Server.Modules.FCMProvider.Parts;
using System.Text.Json.Serialization;

namespace Roadnik.Server.JsonCtx;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(ServiceAccountAuthData))]
[JsonSerializable(typeof(FcmMsg))]
[JsonSerializable(typeof(FirebaseTokenResponse))]
[JsonSerializable(typeof(FcmMasterTokenReqHeader))]
[JsonSerializable(typeof(FcmMasterTokenReqBody))]
internal partial class FcmPushJsonCtx : JsonSerializerContext
{

}
