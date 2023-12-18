using Ax.Fw.SharedTypes.Interfaces;
using JustLogger.Interfaces;

namespace Roadnik.MAUI.Toolkit;

public abstract class CAndroidService : Android.App.Service
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
  public IReadOnlyDependencyContainer Container { get; private set; }
  public ILogger Log { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

  public override void OnCreate()
  {
    base.OnCreate();

    Container = MauiProgram.Container;
    Log = Container.Locate<ILogger>()[Class.SimpleName];
  }
  public override void OnDestroy()
  {
    base.OnDestroy();
  }
}
