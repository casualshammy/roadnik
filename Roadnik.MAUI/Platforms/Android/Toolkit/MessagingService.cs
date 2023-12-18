using Android.Gms.Common;
using Firebase.Messaging;

namespace Roadnik.MAUI.Platforms.Android.Toolkit;

public static class MessagingService
{
  public static bool IsMessagingAvailable()
  {
    var result = GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(global::Android.App.Application.Context);
    return result == ConnectionResult.Success;
  }

  public static async Task<string?> GetTokenAsync()
  {
    var token = await Task.Run(() => (string?)FirebaseMessaging.Instance.GetToken().Result);
    return token;
  }

  public static void SubscribeToTopic(string _topic) => FirebaseMessaging.Instance.SubscribeToTopic(_topic);

  public static void UnsubscribeFromTopic(string _topic) => FirebaseMessaging.Instance.UnsubscribeFromTopic(_topic);

}
