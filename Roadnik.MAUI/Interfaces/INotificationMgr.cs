using Newtonsoft.Json.Linq;
using Roadnik.MAUI.Data;

namespace Roadnik.MAUI.Interfaces;

internal interface INotificationMgr
{
  IObservable<NotificationTapEvent> Events { get; }

  void ShowNotification(
    int _notificationId,
    string _title,
    string _msg,
    string? _channelId = null,
    JToken? _returningData = null);
}
