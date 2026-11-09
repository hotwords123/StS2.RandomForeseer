namespace RandomForeseer.Common;

internal static class TaskExtensions
{
    /// <summary>
    /// Executes an action after a task completes, regardless of whether the task completed successfully or faulted.
    /// </summary>
    public static async Task<T> WithFinally<T>(this Task<T> task, Action onFinally)
    {
        try
        {
            return await task;
        }
        finally
        {
            onFinally();
        }
    }

    /// <summary>
    /// Executes an action after a task completes, regardless of whether the task completed successfully or faulted.
    /// </summary>
    public static async Task WithFinally(this Task task, Action onFinally)
    {
        try
        {
            await task;
        }
        finally
        {
            onFinally();
        }
    }
}
