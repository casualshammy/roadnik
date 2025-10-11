using Android.Content;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI.Platforms.Android.BroadcastReceivers;

[BroadcastReceiver(Enabled = true)]
public class StopLocationSharingReceiver : BroadcastReceiver
{
  public override void OnReceive(
    Context? _ctx,
    Intent? _intent)
  {
    if (_intent?.Action == Consts.INTENT_STOP_LOC_SHARING)
    {
      var locationReporter = MauiProgram.Container.Locate<ILocationReporter>();
      locationReporter.SetState(false);
    }
  }
}
