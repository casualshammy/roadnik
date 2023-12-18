using Android.Util;
using JustLogger;
using JustLogger.Interfaces;
using JustLogger.Toolkit;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Roadnik.MAUI.Platforms.Android.Toolkit;

internal class AndroidLogger : ILogger
{
  private readonly string p_tag;
  private readonly ConcurrentDictionary<LogEntryType, long> p_stats = new();

  ILogger ILogger.this[string _scope] => throw new NotImplementedException();

  public AndroidLogger(string _tag)
  {
    p_tag = _tag;
  }

  public NamedLogger this[string _name] => new(this, _name);

  public void Error(string _text, string? _name = null)
  {
    p_stats.AddOrUpdate(LogEntryType.ERROR, 1, (_, _prevValue) => ++_prevValue);
    Log.Error(p_tag, _text);
  }

  public void Error(string _text, Exception _ex, string? _name = null)
  {
    p_stats.AddOrUpdate(LogEntryType.ERROR, 1, (_, _prevValue) => ++_prevValue);
    Log.Error(p_tag, $"{_text}{Environment.NewLine}{_ex.Message}");
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

  public void NewEvent(LogEntryType _type, string _text)
  {
    p_stats.AddOrUpdate(_type, 1, (_, _prevValue) => ++_prevValue);

    if (_type == LogEntryType.INFO)
      Info(_text);
    else if (_type == LogEntryType.ERROR)
      Error(_text);
    else if (_type == LogEntryType.WARN)
      Warn(_text);
  }

  public void Flush() { }

}
