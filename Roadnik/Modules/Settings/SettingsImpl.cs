using Newtonsoft.Json;
using Roadnik.Interfaces;

namespace Roadnik.Modules.Settings;

public class SettingsImpl : ISettings
{
  public SettingsImpl(
    [JsonProperty(nameof(WebrootDirPath))] string? _webrootDirPath,
    [JsonProperty(nameof(LogDirPath))] string? _logDirPath,
    [JsonProperty(nameof(DataDirPath))] string? _dataDirPath,
    [JsonProperty(nameof(ThunderforestApikey))] string? _trunderforestApikey,
    [JsonProperty(nameof(ThunderforestCacheSize))] long? _thunderforestCacheSize,
    [JsonProperty(nameof(PortBind))] int? _portBind,
    [JsonProperty(nameof(IpBind))] string? _ipBind,
    [JsonProperty(nameof(AdminApiKey))] string? _adminApiKey,
    [JsonProperty(nameof(AllowAnonymousPublish))] bool? _allowAnonymousPublish,
    [JsonProperty(nameof(AnonymousMaxPoints))] int? _anonymousMaxPoints,
    [JsonProperty(nameof(RegisteredMaxPoints))] int? _registeredMaxPoints,
    [JsonProperty(nameof(AnonymousMinIntervalMs))] double? _anonymousMinIntervalMs,
    [JsonProperty(nameof(RegisteredMinIntervalMs))] double? _registeredMinIntervalMs,
    [JsonProperty(nameof(GetRequestReturnsEntriesCount))] int? _getRequestReturnsEntriesCount,
    [JsonProperty(nameof(FCMServiceAccountJsonPath))] string? _fCMServiceAccountJsonPath,
    [JsonProperty(nameof(FCMProjectId))] string? _fCMProjectId)
  {
    WebrootDirPath = _webrootDirPath ?? "www";
    LogDirPath = _logDirPath ?? "logs";
    DataDirPath = _dataDirPath ?? "data";
    ThunderforestApikey = _trunderforestApikey;
    PortBind = _portBind ?? 5544;
    IpBind = _ipBind ?? "0.0.0.0";
    AdminApiKey = _adminApiKey;
    ThunderforestCacheSize = _thunderforestCacheSize ?? 0;
    AllowAnonymousPublish = _allowAnonymousPublish ?? true;
    AnonymousMaxPoints = _anonymousMaxPoints ?? 100;
    RegisteredMaxPoints = _registeredMaxPoints ?? 1000;
    AnonymousMinIntervalMs = _anonymousMinIntervalMs ?? 9.9d * 1000;
    RegisteredMinIntervalMs = _registeredMinIntervalMs ?? 0.9 * 1000;
    GetRequestReturnsEntriesCount = _getRequestReturnsEntriesCount ?? 10000;
    FCMServiceAccountJsonPath = _fCMServiceAccountJsonPath;
    FCMProjectId = _fCMProjectId;
  }

  public string WebrootDirPath { get; }
  public string LogDirPath { get; }
  public string DataDirPath { get; }
  public string? ThunderforestApikey { get; }
  public long ThunderforestCacheSize { get; }
  public int PortBind { get; }
  public string IpBind { get; }
  public string? AdminApiKey { get; }
  public bool AllowAnonymousPublish { get; }
  public int AnonymousMaxPoints { get; }
  public int RegisteredMaxPoints { get; }
  public double AnonymousMinIntervalMs { get; }
  public double RegisteredMinIntervalMs { get; }
  public int GetRequestReturnsEntriesCount { get; }
  public string? FCMServiceAccountJsonPath { get; }
  public string? FCMProjectId { get; }

  public int GetWebMaxPoints() => Math.Max(AnonymousMaxPoints, RegisteredMaxPoints);

}