using Roadnik.Modules.FCMProvider;
using Roadnik.Server.Modules.FCMProvider.Parts;
using System.Text.Json.Serialization;

namespace AxToolsServerNet.Data.Serializers;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(ServiceAccountAuthData))]
[JsonSerializable(typeof(FcmMsg))]
[JsonSerializable(typeof(FirebaseTokenResponse))]
[JsonSerializable(typeof(FcmMasterTokenReqHeader))]
[JsonSerializable(typeof(FcmMasterTokenReqBody))]
internal partial class FcmPushJsonCtx : JsonSerializerContext
{

}
