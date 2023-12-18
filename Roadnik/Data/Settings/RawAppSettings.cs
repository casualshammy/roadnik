namespace Roadnik.Server.Data.Settings;

public class RawAppSettings
{
  public string? WebrootDirPath { get; init; }
  public string? LogDirPath { get; init; }
  public string? DataDirPath { get; init; }
  public string? ThunderforestApikey { get; init; }
  public long? ThunderforestCacheSize { get; init; }
  public int? PortBind { get; init; }
  public string? IpBind { get; init; }
  public string? AdminApiKey { get; init; }
  public bool? AllowAnonymousPublish { get; init; }
  public int? AnonymousMaxPoints { get; init; }
  public double? AnonymousMinIntervalMs { get; init; }
  public int? RegisteredMaxPoints { get; init; }
  public double? RegisteredMinIntervalMs { get; init; }
  public string? FCMServiceAccountJsonPath { get; init; }
  public string? FCMProjectId { get; init; }
}
