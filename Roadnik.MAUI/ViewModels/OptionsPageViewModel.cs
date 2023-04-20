using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI.ViewModels;

internal class OptionsPageViewModel : BaseViewModel
{
  private readonly IPreferencesStorage p_storage;
  private string? p_serverName;
  private string? p_serverKey;
  private int p_minimumTime;
  private int p_minimumDistance;

  public OptionsPageViewModel()
  {
    p_storage = Container.Locate<IPreferencesStorage>();
    p_serverName = p_storage.GetValueOrDefault<string>(p_storage.SERVER_ADDRESS);
    p_serverKey = p_storage.GetValueOrDefault<string>(p_storage.SERVER_KEY);
    p_minimumTime = p_storage.GetValueOrDefault<int>(p_storage.TIME_INTERVAL);
    p_minimumDistance = p_storage.GetValueOrDefault<int>(p_storage.DISTANCE_INTERVAL);
  }

  public string Title { get; } = "Options";
  public string? ServerName
  {
    get => p_serverName;
    set
    {
      SetProperty(ref p_serverName, value);
      if (p_serverName != null)
        p_storage.SetValue(p_storage.SERVER_ADDRESS, p_serverName);
    }
  }
  public string? ServerKey
  {
    get => p_serverKey;
    set
    {
      SetProperty(ref p_serverKey, value);
      if (p_serverKey != null)
        p_storage.SetValue(p_storage.SERVER_KEY, p_serverKey);
    }
  }
  public int MinimumTime
  {
    get => p_minimumTime;
    set
    {
      SetProperty(ref p_minimumTime, value);
      p_storage.SetValue(p_storage.TIME_INTERVAL, p_minimumTime);
    }
  }
  public int MinimumDistance
  {
    get => p_minimumDistance;
    set
    {
      SetProperty(ref p_minimumDistance, value);
      p_storage.SetValue(p_storage.DISTANCE_INTERVAL, p_minimumDistance);
    }
  }

}
