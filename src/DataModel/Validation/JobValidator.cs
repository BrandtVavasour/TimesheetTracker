using FluentValidation;

namespace TimesheetTracker.DataModel.Validation;

/// <summary>Validation rules for a job edited on the Jobs screen.</summary>
public sealed class JobValidator : AbstractValidator<Job>
{
    public JobValidator()
    {
        RuleFor(j => j.Name)
            .NotEmpty().WithMessage("Give the job a name.")
            .MaximumLength(200);

        RuleFor(j => j.DecimalPlaces)
            .InclusiveBetween(0, 4).WithMessage("Decimal places must be between 0 and 4.");

        RuleForEach(j => j.CustomFields).ChildRules(f =>
        {
            f.RuleFor(x => x.Name).NotEmpty().WithMessage("Custom fields need a label.").MaximumLength(100);
            f.RuleFor(x => x.Value).MaximumLength(500);
        });

        RuleForEach(j => j.ProjectCodes).ChildRules(c =>
        {
            c.RuleFor(x => x.Code).NotEmpty().WithMessage("Project codes need a code.").MaximumLength(100);
            c.RuleFor(x => x.Description).MaximumLength(500);
        });
    }
}
