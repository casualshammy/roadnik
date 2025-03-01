using Android.Gms.Common;
using Ax.Fw.SharedTypes.Interfaces;
using Firebase.Messaging;
using Roadnik.MAUI.Toolkit;

namespace Roadnik.MAUI.Platforms.Android.Toolkit;

public static class MessagingService
{
  static MessagingService()
  {
    var app = Firebase.FirebaseApp.InitializeApp(global::Android.App.Application.Context);
    if (app == null)
    {
      var log = MauiProgram.Container?.Locate<ILog>();
      log?.Error("Could not initialize Firebase app: init method returned null!");
    }
  }

  public static bool IsMessagingAvailable()
  {
    var result = GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(global::Android.App.Application.Context);
    return result == ConnectionResult.Success;
  }

  public static async Task<string?> GetTokenAsync()
  {
    var token = (string?)await FirebaseMessaging.Instance.GetToken().AsAsyncTask();
    return token;
  }

  public static void SubscribeToTopic(string _topic) => FirebaseMessaging.Instance.SubscribeToTopic(_topic);

  public static void UnsubscribeFromTopic(string _topic) => FirebaseMessaging.Instance.UnsubscribeFromTopic(_topic);

}
