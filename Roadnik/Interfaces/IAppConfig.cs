using System.Net;

namespace Roadnik.Server.Interfaces;

internal interface IAppConfig
{
  public string WebrootDirPath { get; init; }
  public string LogDirPath { get; init; }
  public string DataDirPath { get; init; }
  public IPAddress BindIp { get; init; }
  public int BindPort { get; init; }
  public uint MaxPathPointsPerRoom { get; init; }
  public double MaxPathPointAgeHours { get; init; }
  public uint MinPathPointIntervalMs { get; init; }
  public string FirebaseServiceAccountJsonPath { get; init; }
  public string FirebaseProjectId { get; init; }
  public string? ThunderforestApiKey { get; init; }
  public long? MapTilesCacheSize { get; init; }
  public string? AdminApiKey { get; init; }
  string? StravaTilesRideUrl { get; init; }
  string? StravaTilesRunUrl { get; init; }
  IReadOnlyDictionary<string, string> StravaTilesHeaders { get; init; }
}
