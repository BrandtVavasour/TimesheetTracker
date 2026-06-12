using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimesheetTracker.Web.Services;

/// <summary>
/// Tracks whether an editable model has unsaved changes by comparing it against
/// a value snapshot taken when the form loaded (or last saved). Lets forms gate
/// the Save button and a navigation guard on "dirty" without bespoke per-form
/// comparison code.
/// </summary>
public sealed class FormSnapshot<T>
{
    private static readonly JsonSerializerOptions Options = new()
    {
        // EF entities can have navigation back-references; don't choke on cycles.
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    private string _baseline;

    public FormSnapshot(T model) => _baseline = Serialize(model);

    /// <summary>True when the model differs from the last captured baseline.</summary>
    public bool IsDirty(T model) => Serialize(model) != _baseline;

    /// <summary>Re-baseline to the current state (e.g. after a successful save).</summary>
    public void Reset(T model) => _baseline = Serialize(model);

    private static string Serialize(T model) => JsonSerializer.Serialize(model, Options);
}
