using FluentValidation;
using TimesheetTracker.DataModel.Enums;

namespace TimesheetTracker.Web.Validation;

/// <summary>Editable view model for the Profile screen.</summary>
public sealed class ProfileForm
{
    public string DisplayName { get; set; } = "";
    public AustralianState DefaultState { get; set; }
    public bool ExportIncludeAllDays { get; set; } = true;
}

public sealed class ProfileFormValidator : AbstractValidator<ProfileForm>
{
    public ProfileFormValidator() =>
        RuleFor(p => p.DisplayName)
            .NotEmpty().WithMessage("Enter a display name.")
            .MaximumLength(100);
}
