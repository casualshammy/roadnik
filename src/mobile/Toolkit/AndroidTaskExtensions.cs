namespace Roadnik.MAUI.Toolkit;

internal static class AndroidTaskExtensions
{
  private class TaskCompleteListener : Java.Lang.Object, Android.Gms.Tasks.IOnCompleteListener
  {
    private readonly TaskCompletionSource<Java.Lang.Object> p_tcs;

    public TaskCompleteListener(
      TaskCompletionSource<Java.Lang.Object> _tcs)
    {
      p_tcs = _tcs;
    }

    public void OnComplete(Android.Gms.Tasks.Task _task)
    {
      if (_task.IsCanceled)
        p_tcs.SetCanceled();
      else if (_task.IsSuccessful)
        p_tcs.SetResult(_task.Result);
      else if (_task.Exception != null)
        p_tcs.SetException(_task.Exception);
    }
  }

  public static Task<Java.Lang.Object> AsAsyncTask(this Android.Gms.Tasks.Task _task)
  {
    var tcs = new TaskCompletionSource<Java.Lang.Object>();
    _task.AddOnCompleteListener(new TaskCompleteListener(tcs));
    return tcs.Task;
  }

}
