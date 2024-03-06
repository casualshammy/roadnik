using Android.Util;
using Ax.Fw.Log;
using Ax.Fw.SharedTypes.Data.Log;

namespace Roadnik.MAUI.Platforms.Android.Toolkit;

internal static class AndroidLoggerExtension
{
  public static GenericLog AttachAndroidLog(this GenericLog _log, string _tag)
  {
    var subs = _log.LogEntries
      .Subscribe(_entry =>
      {
        if (_entry.Type == LogEntryType.INFO)
          Log.Info(_tag, _entry.Text);
        else if (_entry.Type == LogEntryType.WARN)
          Log.Warn(_tag, _entry.Text);
        else if (_entry.Type == LogEntryType.ERROR)
          Log.Error(_tag, _entry.Text);
      });
    _log.AddEndAction(() => subs.Dispose());

    return _log;
  }
}
