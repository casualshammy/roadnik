namespace Roadnik.Interfaces;

public interface ISettings
{
  string WebrootDirPath { get; }
  string LogDirPath { get; }
  string DataDirPath { get; }
  int PortBind { get; }
  string IpBind { get; }
  string? ThunderforestApikey { get; }
  string? AdminApiKey { get; }
  long ThunderforestCacheSize { get; }
  bool AllowAnonymousPublish { get; }
  int AnonymousMaxPoints { get; }
  int RegisteredMaxPoints { get; }
  double AnonymousMinIntervalMs { get; }
  double RegisteredMinIntervalMs { get; }
  int GetRequestReturnsEntriesCount { get; }
  string? FCMServiceAccountJsonPath { get; }
  string? FCMProjectId { get; }
}