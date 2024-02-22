namespace Roadnik.MAUI.Toolkit;

internal static class MainThreadExt
{
  public static async Task<bool> InvokeAsync(
    Action<CancellationToken> _func,
    TimeSpan? _timeout = null,
    CancellationToken _ct = default)
  {
    if (MainThread.IsMainThread)
    {
      _func(_ct);
      return true;
    }

    using var semaphore = new SemaphoreSlim(0, 1);

    MainThread.BeginInvokeOnMainThread(() =>
    {
      try
      {
        _func(_ct);
      }
      finally
      {
        try
        {
          semaphore.Release();
        }
        catch (ObjectDisposedException) { }
      }
    });

    var result = await semaphore.WaitAsync(_timeout ?? TimeSpan.FromSeconds(1), _ct);
    return result;
  }

  public static bool Invoke(
    Action _func,
    TimeSpan? _timeout = null)
  {
    if (MainThread.IsMainThread)
    {
      _func();
      return true;
    }

    using var semaphore = new SemaphoreSlim(0, 1);

    MainThread.BeginInvokeOnMainThread(() =>
    {
      try
      {
        _func();
      }
      finally
      {
        try
        {
          semaphore.Release();
        }
        catch (ObjectDisposedException) { }
      }
    });

    var result = semaphore.Wait(_timeout ?? TimeSpan.FromSeconds(1));
    return result;
  }
}
