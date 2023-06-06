#if ANDROID
using Ax.Fw.Attributes;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using JustLogger.Interfaces;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Platforms.Android.Toolkit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Modules.FirebaseMessaging;

[ExportClass(typeof(FirebaseMessagingImpl), Singleton: true, ActivateOnStart: true)]
internal class FirebaseMessagingImpl
{
  public FirebaseMessagingImpl(
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
}

#endif