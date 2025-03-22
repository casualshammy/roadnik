namespace Roadnik.Server.Data.Settings;

internal record AppConfig
{
  public required string WebrootDirPath { get; init; }
  public required string LogDirPath { get; init; }
  public required string DataDirPath { get; init; }
  public required string IpBind { get; init; }
  public required int PortBind { get; init; }
  public required string? ThunderforestApiKey { get; init; }
  public required long? MapTilesCacheSize { get; init; }
  public required string? AdminApiKey { get; init; }
  public required uint? MaxPathPointsPerRoom { get; init; }
  public required double? MaxPathPointAgeHours { get; init; }
  public required uint? MinPathPointIntervalMs { get; init; }
  public required string? FCMServiceAccountJsonPath { get; init; }
  public required string? FCMProjectId { get; init; }

  public static AppConfig From(RawAppSettings _rawConfig)
  {
    return new AppConfig
    {
      WebrootDirPath = _rawConfig.WebrootDirPath,
      LogDirPath = _rawConfig.LogDirPath,
      DataDirPath = _rawConfig.DataDirPath,
      IpBind = _rawConfig.IpBind,
      PortBind = _rawConfig.PortBind,
      ThunderforestApiKey = _rawConfig.ThunderforestApiKey,
      MapTilesCacheSize = _rawConfig.MapTilesCacheSize,
      AdminApiKey = _rawConfig.AdminApiKey,
      MaxPathPointsPerRoom = _rawConfig.MaxPathPointsPerRoom,
      MaxPathPointAgeHours = _rawConfig.MaxPathPointAgeHours,
      MinPathPointIntervalMs = _rawConfig.MinPathPointIntervalMs,
      FCMServiceAccountJsonPath = _rawConfig.FCMServiceAccountJsonPath,
      FCMProjectId = _rawConfig.FCMProjectId,
    };
  }

}
