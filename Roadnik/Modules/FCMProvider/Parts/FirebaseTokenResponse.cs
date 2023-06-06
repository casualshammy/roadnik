using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Roadnik.Modules.FCMProvider.Parts;

internal record FirebaseTokenResponse(
  [property: JsonProperty("access_token")] string AccessToken,
  [property: JsonProperty("token_type")] string TokenType,
  [property: JsonProperty("expires_in")] int ExpiresIn);
