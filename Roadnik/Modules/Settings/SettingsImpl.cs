using Newtonsoft.Json;
using Roadnik.Interfaces;

namespace Roadnik.Modules.Settings;

internal class SettingsImpl : ISettings
{
  public SettingsImpl(
    [JsonProperty(nameof(WebrootDirPath))] string? _webrootDirPath,
    [JsonProperty(nameof(LogDirPath))] string? _logDirPath,
    [JsonProperty(nameof(DataDirPath))] string? _dataDirPath,
    [JsonProperty(nameof(ThunderforestApikey))] string? _trunderforestApikey,
    [JsonProperty(nameof(PortBind))] int? _portBind,
    [JsonProperty(nameof(IpBind))] string? _ipBind,
    [JsonProperty(nameof(AdminApiKey))] string? _adminApiKey)
  {
    WebrootDirPath = _webrootDirPath ?? "www";
    LogDirPath = _logDirPath ?? "logs";
    DataDirPath = _dataDirPath ?? "data";
    ThunderforestApikey = _trunderforestApikey;
    PortBind = _portBind ?? 5544;
    IpBind = _ipBind ?? "0.0.0.0";
    AdminApiKey = _adminApiKey;
  }

  public string WebrootDirPath { get; }

  public string LogDirPath { get; }

  public string DataDirPath { get; }

  public string? ThunderforestApikey { get; }

  public int PortBind { get; }

  public string IpBind { get; }

  public string? AdminApiKey { get; }

}