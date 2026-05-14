using Ax.Fw.SharedTypes.Interfaces;

namespace Roadnik.MAUI.Toolkit;

public abstract class CAndroidService : Android.App.Service
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
  public IReadOnlyDependencyContainer Container { get; private set; }
  public ILog Log { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

  public override void OnCreate()
  {
    base.OnCreate();

    Container = MauiProgram.Container;
    Log = Container.Locate<ILog>()[Class.SimpleName];
  }
  public override void OnDestroy()
  {
    base.OnDestroy();
  }
}
