namespace Wessl.App.Services;

public readonly record struct LogOperationResult(string Message, bool IsSuccess);

public static class LogOperation
{
    public static async Task<LogOperationResult> RunAsync(
        Func<Task> operation,
        string successMessage,
        Func<Exception, string> errorMessage)
    {
        try
        {
            await operation().ConfigureAwait(false);
            return new(successMessage, true);
        }
        catch (Exception exception)
        {
            return new(errorMessage(exception), false);
        }
    }
}
