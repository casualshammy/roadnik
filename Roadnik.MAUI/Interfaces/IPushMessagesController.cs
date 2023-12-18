using Roadnik.MAUI.Data;

namespace Roadnik.MAUI.Interfaces;

internal interface IPushMessagesController
{
  IObservable<PushNotificationEvent> PushMessages { get; }

  void AddPushMsg(PushNotificationEvent _event);
}
