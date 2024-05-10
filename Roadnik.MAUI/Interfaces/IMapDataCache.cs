using Ax.Fw.Cache;

namespace Roadnik.MAUI.Interfaces;

public interface IMapDataCache
{
  FileCache Cache { get; }

  void EnqueueDownload(string _url);
  Stream? GetStream(string _url);
}