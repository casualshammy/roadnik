using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Server.Interfaces;

namespace Roadnik.Server.Data.WebServer;

internal class ScopedLog : IScopedLog
{
  private readonly ILog p_log;

  public ScopedLog(ILog _log)
  {
    p_log = _log;
  }

  public void Info(string _message) => p_log.Info(_message);
  public void Warn(string _message) => p_log.Warn(_message);
  public void Error(string _message) => p_log.Error(_message);

  public ILog this[string _scope] => p_log[_scope];

}
