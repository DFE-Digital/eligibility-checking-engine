using FluentValidation;

public class EligibilityCheckReportRequestValidator : AbstractValidator<EligibilityCheckReportRequest>
{
    public EligibilityCheckReportRequestValidator()
    {
        RuleFor(x => x.StartDate)
            .NotNull()
            .WithMessage("Start Date is required");

        RuleFor(x => x.EndDate)
            .NotEmpty()
            .WithMessage("End Date is required");
        
        RuleFor(x => x.StartDate)
            .LessThanOrEqualTo(x => x.EndDate)
            .WithMessage("Start Date must be less than or equal to End Date");
        
        RuleFor(x => x.StartDate)
            .Must(date => date <= DateTime.UtcNow)
            .WithMessage("Start Date must be in the past or present");
        
        RuleFor(x => x.EndDate)
            .Must(date => date <= DateTime.UtcNow)
            .WithMessage("End Date must be in the past or present");
        
    }
}