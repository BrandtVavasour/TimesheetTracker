namespace TimesheetTracker.Web.Services;

/// <summary>A transient notification raised after an action completes.</summary>
public sealed record ToastItem(Guid Id, string Message, string Tone);

/// <summary>
/// Sonner-style outcome notifications. Pages report success/failure here and
/// the <c>ToastHost</c> in the layout renders + auto-dismisses them.
/// Scoped per circuit.
/// </summary>
public interface IToastService
{
    IReadOnlyList<ToastItem> Items { get; }
    event Action? Changed;
    void Success(string message, int durationMs = 3500);
    void Error(string message, int durationMs = 6500);
    void Dismiss(Guid id);
}

public sealed class ToastService : IToastService
{
    private readonly object gate = new();
    private readonly List<ToastItem> items = [];

    public IReadOnlyList<ToastItem> Items
    {
        get { lock (gate) return [.. items]; }
    }

    public event Action? Changed;

    public void Success(string message, int durationMs = 3500) => Show(message, "success", durationMs);

    public void Error(string message, int durationMs = 6500) => Show(message, "error", durationMs);

    public void Dismiss(Guid id)
    {
        lock (gate)
        {
            if (items.RemoveAll(t => t.Id == id) == 0) return;
        }
        Changed?.Invoke();
    }

    private void Show(string message, string tone, int durationMs)
    {
        var item = new ToastItem(Guid.NewGuid(), message, tone);
        lock (gate) items.Add(item);
        Changed?.Invoke();
        _ = AutoDismissAsync(item.Id, durationMs);
    }

    private async Task AutoDismissAsync(Guid id, int durationMs)
    {
        await Task.Delay(durationMs);
        Dismiss(id);
    }
}
