using Ax.Fw.Attributes;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Newtonsoft.Json.Linq;
using Plugin.LocalNotification;
using Plugin.LocalNotification.EventArgs;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using System.Collections.Concurrent;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Modules.NotificationManager;

[ExportClass(typeof(INotificationMgr), Singleton: true)]
internal class NotificationMgrImpl : INotificationMgr
{
  private readonly ReplaySubject<NotificationTapEvent> p_tapEvents = new(1);
  private readonly ConcurrentStack<NotificationTapEvent> p_eventsStack = new();

  public NotificationMgrImpl(IReadOnlyLifetime _lifetime)
  {
    LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationActionTapped;
    _lifetime.DoOnEnded(() => LocalNotificationCenter.Current.NotificationActionTapped -= OnNotificationActionTapped);

    Events = p_tapEvents;
    EventsStack = p_eventsStack;
  }

  public IObservable<NotificationTapEvent> Events { get; }
  public IProducerConsumerCollection<NotificationTapEvent> EventsStack { get; }

  public void ShowNotification(
    int _notificationId, 
    string _title, 
    string _msg,
    string? _channelId = null,
    JToken? _returningData = null)
  {
    var notification = new NotificationRequest
    {
      NotificationId = _notificationId,
      Title = _title,
      Description = _msg,
      Android =
      {
        IconSmallName =
        {
          ResourceName = "letter_r",
        }
      }
    };

    if (_returningData != null)
      notification.ReturningData = _returningData.ToString();

    if (!_channelId.IsNullOrWhiteSpace())
      notification.Android.ChannelId = _channelId;

    LocalNotificationCenter.Current.Show(notification);
  }

  private void OnNotificationActionTapped(NotificationActionEventArgs _e)
  {
    if (_e.IsDismissed)
      return;

    var data = new NotificationTapEvent(
      Environment.TickCount64,
      _e.Request.NotificationId, 
      _e.IsDismissed, 
      JToken.Parse(_e.Request.ReturningData));

    if (p_eventsStack.Count > 100)
      p_eventsStack.Clear();

    p_eventsStack.Push(data);
    p_tapEvents.OnNext(data);
  }

}
