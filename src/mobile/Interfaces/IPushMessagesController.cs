using Roadnik.MAUI.Data;
using System.Collections.Concurrent;
using System.Reactive;

namespace Roadnik.MAUI.Interfaces;

internal interface IPushMessagesController
{
  IProducerConsumerCollection<PushNotificationEvent> Notifications { get; }
  IObservable<Unit> OnNewNotification { get; }

  void AddPushMsg(PushNotificationEvent _event);
}
