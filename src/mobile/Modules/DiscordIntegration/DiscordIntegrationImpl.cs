using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Data.DiscordIntegration;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.JsonCtx;
using Roadnik.MAUI.Modules.DiscordIntegration.Parts;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using static Roadnik.MAUI.Data.AppConsts;

namespace Roadnik.MAUI.Modules.DiscordIntegration;

internal class DiscordIntegrationImpl : IDiscordIntegration, IAppModule<IDiscordIntegration>
{
  public static IDiscordIntegration ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      IReadOnlyLifetime _lifetime,
      ILog _log,
      IPreferencesStorage _storage,
      IHttpClientProvider _httpClientProvider)
      => new DiscordIntegrationImpl(_lifetime, _log["discord-integration"], _storage, _httpClientProvider));
  }

  private readonly ILog p_log;
  private readonly IPreferencesStorage p_storage;
  private readonly IHttpClientProvider p_httpClientProvider;
  private readonly BehaviorSubject<PresenceData?> p_presenceDataSubj = new(null);

  private DiscordIntegrationImpl(
    IReadOnlyLifetime _lifetime,
    ILog _log,
    IPreferencesStorage _storage,
    IHttpClientProvider _httpClientProvider)
  {
    p_log = _log;
    p_storage = _storage;
    p_httpClientProvider = _httpClientProvider;

    p_storage.PreferencesChanged
      .Select(_ =>
      {
        var isEnabled = _storage.GetValueOrDefault(PREF_DISCORD_ENABLED, PrefsStorageJsonCtx.Default.Boolean);
        var encToken = _storage.GetValueOrDefault(PREF_DISCORD_TOKEN, PrefsStorageJsonCtx.Default.String);
        var appId = _storage.GetValueOrDefault(PREF_APP_INSTALLATION_ID, PrefsStorageJsonCtx.Default.Guid);
        return (Enabled: isEnabled, EncToken: encToken, AppId: appId);
      })
      .CombineLatest(p_presenceDataSubj)
      .DistinctUntilChanged(_ =>
      {
        var (data, presenceData) = _;
        return HashCode.Combine(data.Enabled, data.EncToken, presenceData?.SessionId);
      })
      .Throttle(TimeSpan.FromSeconds(1))
      .HotAlive(_lifetime, null, (_e, _life) =>
      {
        var (data, presenceData) = _e;
        var token = TryLoadToken();
        var isEnabled = data.Enabled;
        var active = isEnabled && token != null && presenceData != null;

        if (!active)
          return;

        var discordClient = new DiscordWebsocketClient(
          _life, 
          _log["discord-client"], 
          p_httpClientProvider, 
          _storage, 
          p_presenceDataSubj, 
          token!);

        discordClient.StartLoop(RevokeAuth);
      });
  }

  public void RevokeAuth()
  {
    try
    {
      p_storage.RemoveValue(PREF_DISCORD_TOKEN);
      p_storage.SetValue(PREF_DISCORD_ENABLED, false, PrefsStorageJsonCtx.Default.Boolean);
      p_log.Info($"Discord auth revoked");
    }
    catch (Exception ex)
    {
      p_log.Error($"Error revoking Discord auth", ex);
    }
  }

  public void UpdatePresence(
    int _sessionId,
    double _lat,
    double _lng,
    string _roomId,
    float? _speed,
    int? _hrm)
    => p_presenceDataSubj.OnNext(new PresenceData(_sessionId, _lat, _lng, _roomId, _speed, _hrm));

  public void ClearPresence() => p_presenceDataSubj.OnNext(null);

  private string? TryLoadToken()
  {
    var appId = p_storage.GetValueOrDefault(PREF_APP_INSTALLATION_ID, PrefsStorageJsonCtx.Default.Guid);
    var encToken = p_storage.GetValueOrDefault(PREF_DISCORD_TOKEN, PrefsStorageJsonCtx.Default.String);

    if (encToken.IsNullOrEmpty())
      return null;

    try
    {
      using var aes = new Ax.Fw.Crypto.AesWithGcm(appId.ToByteArray());
      var tokenDataSpan = aes.Decrypt(Convert.FromBase64String(encToken));
      var tokenData = JsonSerializer.Deserialize(tokenDataSpan, DiscordJsonCtx.Default.String);
      return tokenData;
    }
    catch (Exception ex)
    {
      p_log.Warn($"Error decrypting Discord token from preferences: {ex}");
      return null;
    }
  }

}
