using Ax.Fw.Attributes;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using JustLogger.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Roadnik.Common.ReqRes.PushMessages;
using Roadnik.Interfaces;
using Roadnik.Modules.FCMProvider.Parts;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Roadnik.Modules.FCMProvider;

[ExportClass(typeof(IFCMPublisher), Singleton: true)]
internal class FCMPublisherImpl : IFCMPublisher
{
  private readonly ILogger p_log;
  private readonly JsonSerializerSettings p_camelCaseSerializer;
  private readonly IRxProperty<FCMSettions?> p_fcmSettings;
  private readonly HttpClient p_httpClient = new();
  private volatile FCMAccessToken? p_accessToken;

  public FCMPublisherImpl(
    ISettingsController _settingsController,
    IReadOnlyLifetime _lifetime,
    ILogger _log)
  {
    p_log = _log["fcm-provider"];

    p_camelCaseSerializer = new JsonSerializerSettings()
    {
      ContractResolver = new DefaultContractResolver
      {
        NamingStrategy = new CamelCaseNamingStrategy()
      },
      Formatting = Formatting.Indented
    };

    p_fcmSettings = _settingsController.Value
      .Select(_ =>
      {
        if (_ == null)
          return null;
        if (_.FCMServiceAccountJsonPath.IsNullOrWhiteSpace() || _.FCMProjectId.IsNullOrWhiteSpace())
          return null;

        try
        {
          var json = System.IO.File.ReadAllText(_.FCMServiceAccountJsonPath, Encoding.UTF8);
          var data = JsonConvert.DeserializeObject<ServiceAccountAuthData>(json);
          if (data == null)
            return null;

          return new FCMSettions(data, _.FCMProjectId);
        }
        catch (Exception ex)
        {
          p_log.Error($"Can't deserialize fcm settings!", ex);
          return null;
        }
      })
      .ToProperty(_lifetime);
  }

  public async Task<bool> SendDataAsync(string _topic, PushMsg _pushMsg, CancellationToken _ct)
  {
    var settings = p_fcmSettings.Value;
    if (settings == null)
      return false;

    var payload = new
    {
      Message = new
      {
        Topic = _topic,
        Data = new
        {
          JsonData = JToken.FromObject(_pushMsg).ToString(Formatting.None)
        },
        Android = new
        {
          Ttl = "3600s"
        }
      }
    };

    var json = JsonConvert.SerializeObject(payload, p_camelCaseSerializer);

    using var message = new HttpRequestMessage(
      HttpMethod.Post,
      $"https://fcm.googleapis.com/v1/projects/{settings.ProjectId}/messages:send");

    var token = await GetJwtTokenAsync(settings.Data, _ct);

    message.Headers.Add("Authorization", $"Bearer {token}");
    using var content = new StringContent(json, Encoding.UTF8, "application/json");
    message.Content = content;

    using var response = await p_httpClient.SendAsync(message, _ct);

    if (!response.IsSuccessStatusCode)
      p_log.Error($"Can't send FCM msg, http status code: {response.StatusCode}");

    return response.IsSuccessStatusCode;
  }

  private async Task<string> GetJwtTokenAsync(ServiceAccountAuthData _data, CancellationToken _ct)
  {
    var now = DateTimeOffset.UtcNow;
    var existingToken = p_accessToken;
    if (existingToken != null && existingToken.ValidUntil > now)
      return existingToken.AccessToken;

    p_log.Info($"Generating new FCM access token...");

    using var message = new HttpRequestMessage(HttpMethod.Post, _data.TokenUri);
    using var form = new MultipartFormDataContent();
    var authToken = GetMasterToken(_data);
    using var tokenContent = new StringContent(authToken);
    form.Add(tokenContent, "assertion");
    using var tokenTypeContent = new StringContent("urn:ietf:params:oauth:grant-type:jwt-bearer");
    form.Add(tokenTypeContent, "grant_type");
    message.Content = form;

    using var response = await p_httpClient.SendAsync(message, _ct);
    var content = await response.Content.ReadAsStringAsync(_ct);

    response.EnsureSuccessStatusCode();

    var token = JsonConvert.DeserializeObject<FirebaseTokenResponse>(content, p_camelCaseSerializer);
    if (token == null || token.AccessToken.IsNullOrWhiteSpace() || token.ExpiresIn < 10)
      throw new InvalidOperationException("Token is invalid!");

    var validUntil = now.AddSeconds(token.ExpiresIn - 60);
    p_accessToken = new FCMAccessToken(token.AccessToken, validUntil);

    p_log.Info($"Generated new FCM access token, valid until '{validUntil}'");

    return token.AccessToken;
  }

  private string GetMasterToken(ServiceAccountAuthData _data)
  {
    var now = DateTimeOffset.UtcNow;

    var header = JsonConvert.SerializeObject(new { alg = "RS256", typ = "JWT" }, p_camelCaseSerializer);
    var payload = JsonConvert.SerializeObject(new
    {
      iss = _data.ClientEmail,
      aud = _data.TokenUri,
      scope = "https://www.googleapis.com/auth/firebase.messaging",
      iat = now.ToUnixTimeSeconds(),
      exp = now.ToUnixTimeSeconds() + 3600
    }, p_camelCaseSerializer);

    var headerBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(header));
    var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    var unsignedJwtData = $"{headerBase64}.{payloadBase64}";
    var unsignedJwtBytes = Encoding.UTF8.GetBytes(unsignedJwtData);

    using var rsa = RSA.Create();
    rsa.ImportFromPem(_data.PrivateKey.ToCharArray());

    var signature = rsa.SignData(unsignedJwtBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var signatureBase64 = Convert.ToBase64String(signature);

    return $"{unsignedJwtData}.{signatureBase64}";
  }

}
