using Microsoft.Extensions.Logging;

namespace TimesheetTracker.Web.Services;

/// <summary>
/// Runs a save/mutation with consistent user feedback: success toast on success,
/// logged error + error toast on failure. Replaces the copy-pasted
/// try / Toast.Success / catch+LogError+Toast.Error blocks across the forms.
/// </summary>
public static class ToastActions
{
    public static async Task<bool> RunAsync(
        this IToastService toast, ILogger logger, Func<Task> action,
        string success, string error, string logContext)
    {
        try
        {
            await action();
            toast.Success(success);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Form action failed: {Context}", logContext);
            toast.Error(error);
            return false;
        }
    }
}
