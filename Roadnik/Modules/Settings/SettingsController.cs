using Ax.Fw.Extensions;
using Ax.Fw.JsonStorages;
using Ax.Fw.SharedTypes.Interfaces;
using AxToolsServerNet.Data.Serializers;
using Roadnik.Server.Data.Settings;
using Roadnik.Server.Interfaces;
using System.Reactive.Linq;

namespace Roadnik.Modules.Settings;

internal class SettingsController : ISettingsController
{
  public SettingsController(string _configPath, IReadOnlyLifetime _lifetime)
  {
    var lifetime = _lifetime.GetChildLifetime();
    if (lifetime == null)
      throw new InvalidDataException($"Lifetime is already finished!");

    var storage = new JsonStorage<RawAppSettings>(_configPath, SettingsJsonCtx.Default.RawAppSettings, lifetime);
    Settings = storage
      .Select(_ =>
      {
        if (_ == null)
          return null;

        return AppSettings.FromRawSettings(_);
      })
      .ToProperty(lifetime);
  }

  public IRxProperty<AppSettings?> Settings { get; }

}
