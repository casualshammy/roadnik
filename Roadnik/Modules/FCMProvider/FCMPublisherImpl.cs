using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using AxToolsServerNet.Data.Serializers;
using Roadnik.Common.ReqRes.PushMessages;
using Roadnik.Common.Serializers;
using Roadnik.Interfaces;
using Roadnik.Modules.FCMProvider;
using Roadnik.Modules.FCMProvider.Parts;
using Roadnik.Server.Interfaces;
using Roadnik.Server.Modules.FCMProvider.Parts;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ILog = Ax.Fw.SharedTypes.Interfaces.ILog;

namespace Roadnik.Server.Modules.FCMProvider;

internal class FCMPublisherImpl : IFCMPublisher, IAppModule<IFCMPublisher>
{
  public static IFCMPublisher ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((ISettingsController _settingsController, IReadOnlyLifetime _lifetime, ILog _log) => new FCMPublisherImpl(_settingsController, _lifetime, _log));
  }

  private readonly ILog p_log;
  private readonly IRxProperty<FCMSettions?> p_fcmSettings;
  private readonly HttpClient p_httpClient = new();
  private volatile FCMAccessToken? p_accessToken;

  public FCMPublisherImpl(
    ISettingsController _settingsController,
    IReadOnlyLifetime _lifetime,
    ILog _log)
  {
    p_log = _log["fcm-provider"];

    p_fcmSettings = _settingsController.Settings
      .Select(_ =>
      {
        if (_ == null)
          return null;
        if (_.FCMServiceAccountJsonPath.IsNullOrWhiteSpace() || _.FCMProjectId.IsNullOrWhiteSpace())
          return null;

        try
        {
          var json = File.ReadAllText(_.FCMServiceAccountJsonPath, Encoding.UTF8);
          var data = JsonSerializer.Deserialize(json, FcmPushJsonCtx.Default.ServiceAccountAuthData);
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

    var payload = new FcmMsg()
    {
      Message = new FcmMsgContent()
      {
        Topic = _topic,
        Data = new FcmMsgContentData()
        {
          JsonData = JsonSerializer.Serialize(_pushMsg, AndroidPushJsonCtx.Default.PushMsg)
        },
        Android = new FcmMsgContentAndroid()
        {
          Ttl = "3600s"
        }
      }
    };

    var json = JsonSerializer.Serialize(payload, FcmPushJsonCtx.Default.FcmMsg);

    using var message = new HttpRequestMessage(
      HttpMethod.Post,
      $"https://fcm.googleapis.com/v1/projects/{settings.ProjectId}/messages:send");

    var token = await GetJwtTokenAsync(settings.Data, _ct);

#if DEBUG
    p_log.Warn($"FCM JWT: {token}");
#endif

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

    using var message = new HttpRequestMessage(HttpMethod.Post, _data.token_uri);
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

    var token = JsonSerializer.Deserialize(content, FcmPushJsonCtx.Default.FirebaseTokenResponse);
    if (token == null || token.access_token.IsNullOrWhiteSpace() || token.expires_in < 10)
      throw new InvalidOperationException("Token is invalid!");

    var validUntil = now.AddSeconds(token.expires_in - 60);
    p_accessToken = new FCMAccessToken(token.access_token, validUntil);

    p_log.Info($"Generated new FCM access token, valid until '{validUntil}'");

    return token.access_token;
  }

  private static string GetMasterToken(ServiceAccountAuthData _data)
  {
    var now = DateTimeOffset.UtcNow;

    var header = JsonSerializer.Serialize(new FcmMasterTokenReqHeader("RS256", "JWT"), FcmPushJsonCtx.Default.FcmMasterTokenReqHeader);
    var payload = JsonSerializer.Serialize(new FcmMasterTokenReqBody(
      _data.client_email,
      _data.token_uri,
      "https://www.googleapis.com/auth/firebase.messaging",
      now.ToUnixTimeSeconds(),
      now.ToUnixTimeSeconds() + 3600), FcmPushJsonCtx.Default.FcmMasterTokenReqBody);

    var headerBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(header));
    var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    var unsignedJwtData = $"{headerBase64}.{payloadBase64}";
    var unsignedJwtBytes = Encoding.UTF8.GetBytes(unsignedJwtData);

    using var rsa = RSA.Create();
    rsa.ImportFromPem(_data.private_key.ToCharArray());

    var signature = rsa.SignData(unsignedJwtBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var signatureBase64 = Convert.ToBase64String(signature);

    return $"{unsignedJwtData}.{signatureBase64}";
  }

}
