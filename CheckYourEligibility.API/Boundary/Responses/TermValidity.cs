using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;

namespace CheckYourEligibility.API.Boundary.Responses;

public class TermValidity
{

    public Term? Current { get; set; }
    public Term? Next { get; set; }

    public TermValidity(Term? current, Term? next)
    {
        Current = current ?? new Term(TermName.None, DateTime.MinValue);
        Next = next ?? new Term(TermName.None, DateTime.MinValue);
    }
}


public class Term
{

    public static Term None => new Term(TermName.None, DateTime.MinValue);

    public TermName Name { get; set; }

    public DateTime StartDate { get; set; }

    public Term(TermName name, DateTime startDate)
    {
        Name = name;
        StartDate = startDate;
    }
}