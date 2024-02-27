namespace Roadnik.Server.Data.Settings;

public class AppSettings
{
  private AppSettings() { }

  public required string WebrootDirPath { get; init; }
  public required string LogDirPath { get; init; }
  public required string DataDirPath { get; init; }
  public string? ThunderforestApikey { get; init; }
  public long ThunderforestCacheSize { get; init; }
  public required int PortBind { get; init; }
  public required string IpBind { get; init; }
  public string? AdminApiKey { get; init; }
  public required bool AllowAnonymousPublish { get; init; }
  public required int AnonymousMaxPoints { get; init; }
  public required double AnonymousMinIntervalMs { get; init; }
  public string? FCMServiceAccountJsonPath { get; init; }
  public string? FCMProjectId { get; init; }

  public static AppSettings FromRawSettings(RawAppSettings _rawSettings)
  {
    return new AppSettings()
    {
      WebrootDirPath = _rawSettings.WebrootDirPath ?? "www",
      LogDirPath = _rawSettings.LogDirPath ?? "logs",
      DataDirPath = _rawSettings.DataDirPath ?? "data",
      ThunderforestApikey = _rawSettings.ThunderforestApikey,
      ThunderforestCacheSize = _rawSettings.ThunderforestCacheSize ?? 0,
      PortBind = _rawSettings.PortBind ?? 5544,
      IpBind = _rawSettings.IpBind ?? "127.0.0.1",
      AdminApiKey = _rawSettings.AdminApiKey,
      AllowAnonymousPublish = _rawSettings.AllowAnonymousPublish ?? true,
      AnonymousMaxPoints = _rawSettings.AnonymousMaxPoints ?? 100,
      AnonymousMinIntervalMs = _rawSettings.AnonymousMinIntervalMs ?? 9.9d * 1000,
      FCMServiceAccountJsonPath = _rawSettings.FCMServiceAccountJsonPath,
      FCMProjectId = _rawSettings.FCMProjectId
    };
  }

}