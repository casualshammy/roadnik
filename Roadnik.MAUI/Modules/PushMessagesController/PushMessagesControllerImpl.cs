using Ax.Fw.Attributes;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using JustLogger.Interfaces;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Platforms.Android.Toolkit;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Modules.PushMessagesController;

[ExportClass(typeof(IPushMessagesController), Singleton: true, ActivateOnStart: true)]
internal class PushMessagesControllerImpl : IPushMessagesController
{
  private readonly ReplaySubject<PushNotificationEvent> p_pushMessagesSubj = new(1);

  public PushMessagesControllerImpl(
    IPreferencesStorage _preferencesStorage,
    IReadOnlyLifetime _lifetime,
    ILogger _log)
  {
    var log = _log["firebase-messaging"];
    var retryFlow = new Subject<Unit>();

    _preferencesStorage.PreferencesChanged
      .Merge(retryFlow)
      .Sample(TimeSpan.FromSeconds(5))
      .Scan((string?)null, (_acc, _) =>
      {
        var isAvailable = MessagingService.IsMessagingAvailable();
        if (!isAvailable)
        {
          log.Warn($"Messaging is not available");
          return _acc;
        }

        var roomId = _preferencesStorage.GetValueOrDefault<string>(PREF_ROOM);
        if (roomId != _acc)
        {
          if (!_acc.IsNullOrEmpty())
          {
            try
            {
              MessagingService.UnsubscribeFromTopic(_acc);
              log.Info($"Unsubscribed from topic '{_acc}'");
            }
            catch (Exception ex)
            {
              log.Error($"Can't unsubscribe from topic '{_acc}'", ex);
              retryFlow.OnNext();
              return _acc;
            }
          }

          if (!roomId.IsNullOrEmpty())
          {
            try
            {
              MessagingService.SubscribeToTopic(roomId);
              log.Info($"Subscribed to topic '{roomId}'");
            }
            catch (Exception ex)
            {
              log.Error($"Can't subscribe to topic '{roomId}'", ex);
              retryFlow.OnNext();
              return _acc;
            }
          }
        }
        return roomId;
      })
      .Subscribe(_lifetime);
  }

  public IObservable<PushNotificationEvent> PushMessages => p_pushMessagesSubj;

  public void AddPushMsg(PushNotificationEvent _event) => p_pushMessagesSubj.OnNext(_event);

}
