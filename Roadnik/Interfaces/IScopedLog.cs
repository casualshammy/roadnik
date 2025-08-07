using Ax.Fw.SharedTypes.Interfaces;

namespace Roadnik.Server.Interfaces;

internal interface IScopedLog
{
  ILog this[string _scope] { get; }

  void Error(string _message);
  void Info(string _message);
  void Warn(string _message);
}
