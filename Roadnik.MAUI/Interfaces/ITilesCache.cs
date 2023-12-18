using Ax.Fw.Cache;

namespace Roadnik.MAUI.Interfaces;

public interface ITilesCache
{
  FileCache Cache { get; }

  void EnqueueDownload(string _url);
}