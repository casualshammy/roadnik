using Ax.Fw.Attributes;
using Ax.Fw.Cache;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Interfaces;
using System.Text;

namespace Roadnik.Modules.TilesCache;

[ExportClass(typeof(ITilesCache), Singleton: true)]
internal class TilesCacheImpl : ITilesCache
{
  private readonly FileCache p_cache;

  public TilesCacheImpl(
    ISettings _settings,
    IReadOnlyLifetime _lifetime)
  {
    var folder = Path.Combine(_settings.DataDirPath, "tiles-cache");
    p_cache = new FileCache(
      _lifetime, 
      folder, 
      TimeSpan.FromDays(30), 
      _settings.ThunderforestCacheSize, 
      TimeSpan.FromHours(6));
  }

  public async Task StoreAsync(int _x, int _y, int _z, string _type, Stream _tileStream, CancellationToken _ct)
  {
    var key = GetKey(_x, _y, _z, _type);
    await p_cache.StoreAsync(key, _tileStream, _ct);
  }

  public async Task<Stream?> GetOrDefaultAsync(int _x, int _y, int _z, string _type, CancellationToken _ct)
  {
    var key = GetKey(_x, _y, _z, _type);
    return await p_cache.GetAsync(key, _ct);
  }

  private static string GetKey(int _x, int _y, int _z, string _type)
  {
    var sb = new StringBuilder();
    sb.Append(_x);
    sb.Append('.');
    sb.Append(_y);
    sb.Append('.');
    sb.Append(_z);
    sb.Append('.');
    sb.Append(_type);
    return sb.ToString();
  }

}
