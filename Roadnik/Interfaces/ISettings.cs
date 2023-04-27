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
}