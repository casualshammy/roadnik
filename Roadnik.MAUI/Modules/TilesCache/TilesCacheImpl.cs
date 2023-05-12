using Ax.Fw.Attributes;
using Ax.Fw.Cache;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI.Modules.TilesCache;

[ExportClass(typeof(ITilesCache), Singleton: true)]
internal class TilesCacheImpl : ITilesCache
{
  public TilesCacheImpl(
    IReadOnlyLifetime _lifetime)
  {
    var cacheDir = Path.Combine(FileSystem.Current.CacheDirectory, "tiles-cache");
    var cache = new FileCache(_lifetime, cacheDir, TimeSpan.FromDays(1), 50 * 1024 * 1024, null);
    cache.CleanFiles();
    Cache = cache;
  }

  public FileCache Cache { get; }

}
