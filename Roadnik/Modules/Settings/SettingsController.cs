using Ax.Fw.Extensions;
using Ax.Fw.JsonStorages;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Server.Data.Settings;
using Roadnik.Server.Interfaces;
using Roadnik.Server.JsonCtx;
using System.Reactive.Linq;

namespace Roadnik.Server.Modules.Settings;

internal class SettingsController : ISettingsController
{
  public SettingsController(
    string _configPath,
    ILog _log,
    IReadOnlyLifetime _lifetime)
  {
    var lifetime = _lifetime.GetChildLifetime();
    if (lifetime == null)
      throw new InvalidDataException($"Lifetime is already finished!");

    var storage = new JsonStorage<RawAppSettings>(_configPath, SettingsJsonCtx.Default, lifetime);
    Settings = storage
      .Do(_ => _log.Warn($"New config is read"))
      .Select(_ => _ == null ? null : AppConfig.From(_))
      .ToNullableProperty(lifetime, null);
  }

  public IRxProperty<AppConfig?> Settings { get; }

}
