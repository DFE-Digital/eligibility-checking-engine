using FluentValidation;

public class EligibilityCheckReportRequestValidator : AbstractValidator<EligibilityCheckReportRequest>
{
    public EligibilityCheckReportRequestValidator()
    {

        RuleFor(x => x.StartDate)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Start Date is required")
            .Must(BeValidDate).WithMessage("Start Date must be a valid date");

        RuleFor(x => x.EndDate)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("End Date is required")
            .Must(BeValidDate).WithMessage("End Date must be a valid date");

        RuleFor(x => x.StartDate)
            .Cascade(CascadeMode.Stop)
            .LessThanOrEqualTo(x => x.EndDate)
            .WithMessage("Start Date must be less than or equal to End Date");
        
        RuleFor(x => x.StartDate)
            .Cascade(CascadeMode.Stop)
            .Must(date => DateTime.Parse(date) <= DateTime.UtcNow)
            .WithMessage("Start Date must be in the past or present");
        
        RuleFor(x => x.EndDate)
            .Cascade(CascadeMode.Stop)
            .Must(date => DateTime.Parse(date) <= DateTime.UtcNow)
            .WithMessage("End Date must be in the past or present");

        RuleFor(x => x)
            .Cascade(CascadeMode.Stop)
            .Must(x =>
                DateTime.TryParse(x.StartDate, out var start) &&
                DateTime.TryParse(x.EndDate, out var end) &&
                start <= end)
            .WithMessage("Start Date must be less than or equal to End Date");


    }

    private bool BeValidDate(string date)
    {
        return DateTime.TryParse(date, out _);
    }

}