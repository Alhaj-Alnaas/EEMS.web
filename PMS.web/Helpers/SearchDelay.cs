namespace PMS.web.Helpers;

public sealed class SearchDelay : IDisposable
{
    private CancellationTokenSource? pending;

    public void Cancel() => pending?.Cancel();

    public async Task AfterAsync(Func<Task> action, int milliseconds = 350)
    {
        pending?.Cancel();
        pending?.Dispose();
        var current = new CancellationTokenSource();
        pending = current;
        try
        {
            await Task.Delay(milliseconds, current.Token);
            if (!current.IsCancellationRequested)
                await action();
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        pending?.Cancel();
        pending?.Dispose();
    }
}
