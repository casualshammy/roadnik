using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Roadnik.Modules.FCMProvider.Parts;

internal record ServiceAccountAuthData(
  [property: JsonProperty("type")] string Type,
  [property: JsonProperty("project_id")] string ProjectId,
  [property: JsonProperty("private_key_id")] string PrivateKeyId,
  [property: JsonProperty("private_key")] string PrivateKey,
  [property: JsonProperty("client_email")] string ClientEmail,
  [property: JsonProperty("client_id")] string ClientId,
  [property: JsonProperty("auth_uri")] string AuthUri,
  [property: JsonProperty("token_uri")] string TokenUri,
  [property: JsonProperty("auth_provider_x509_cert_url")] string AuthProviderCertUrl,
  [property: JsonProperty("client_x509_cert_url")] string ClientCertUrl,
  [property: JsonProperty("universe_domain")] string UniverseDomain);
