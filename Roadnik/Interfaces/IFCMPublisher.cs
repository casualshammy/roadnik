using Roadnik.Common.ReqRes.PushMessages;

namespace Roadnik.Interfaces;

public interface IFCMPublisher
{
  Task<bool> SendDataAsync(string _topic, PushMsg _pushMsg, CancellationToken _ct);
}