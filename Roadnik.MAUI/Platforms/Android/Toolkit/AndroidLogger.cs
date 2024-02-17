using Android.Util;
using Ax.Fw.Log;
using Ax.Fw.SharedTypes.Data.Log;
using Ax.Fw.SharedTypes.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Roadnik.MAUI.Platforms.Android.Toolkit;

internal class AndroidLogger : ILogger
{
  private readonly string p_tag;
  private readonly ConcurrentDictionary<LogEntryType, long> p_stats = new();

  public AndroidLogger(string _tag)
  {
    p_tag = _tag;
  }

  public ILogger this[string _name] => new NamedLogger(this, _name);

  public void Error(string _text, Exception? _ex, string? _name = null)
  {
    p_stats.AddOrUpdate(LogEntryType.ERROR, 1, (_, _prevValue) => ++_prevValue);
    Log.Error(p_tag, $"{_text}{Environment.NewLine}{_ex?.Message}");
  }

  public void ErrorJson<T>(string _text, T _object, string? _scope = null) where T : notnull
  {
    p_stats.AddOrUpdate(LogEntryType.ERROR, 1, (_, _prevValue) => ++_prevValue);
    Log.Error(p_tag, $"{_text}{Environment.NewLine}{JsonSerializer.Serialize(_object)}");
  }

  public void Warn(string _text, string? _name = null)
  {
    p_stats.AddOrUpdate(LogEntryType.WARN, 1, (_, _prevValue) => ++_prevValue);
    Log.Warn(p_tag, _text);
  }

  public void WarnJson<T>(string _text, T _object, string? _scope = null) where T : notnull
  {
    p_stats.AddOrUpdate(LogEntryType.WARN, 1, (_, _prevValue) => ++_prevValue);
    Log.Warn(p_tag, $"{_text}{Environment.NewLine}{JsonSerializer.Serialize(_object)}");
  }

  public void Info(string _text, string? _name = null)
  {
    p_stats.AddOrUpdate(LogEntryType.INFO, 1, (_, _prevValue) => ++_prevValue);
    Log.Info(p_tag, _text);
  }

  public void InfoJson<T>(string _text, T _object, string? _scope = null) where T : notnull
  {
    p_stats.AddOrUpdate(LogEntryType.INFO, 1, (_, _prevValue) => ++_prevValue);
    Log.Info(p_tag, $"{_text}{Environment.NewLine}{JsonSerializer.Serialize(_object)}");
  }

  public long GetEntriesCount(LogEntryType _type)
  {
    if (p_stats.TryGetValue(_type, out var count))
      return count;

    return 0L;
  }

  public void Flush() { }

  public void Dispose() { }

}
