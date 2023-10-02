using Ax.Fw.Extensions;
using Ax.Fw.JsonStorages;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Interfaces;

namespace Roadnik.Modules.Settings;

internal class SettingsController : ISettingsController
{
  private readonly JsonObservableStorage<SettingsImpl> p_storage;

  public SettingsController(string _configPath, IReadOnlyLifetime _lifetime)
  {
    var lifetime = _lifetime.GetChildLifetime();
    if (lifetime == null)
      throw new InvalidDataException($"Lifetime is already finished!");

    p_storage = new JsonObservableStorage<SettingsImpl>(lifetime, _configPath);
    Settings = p_storage.ToProperty(lifetime);
  }

  public IRxProperty<SettingsImpl?> Settings { get; }

}
