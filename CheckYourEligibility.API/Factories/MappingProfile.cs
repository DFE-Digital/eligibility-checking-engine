using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using static CheckYourEligibility.API.Boundary.Responses.ApplicationResponse;
using ApplicationEvidence = CheckYourEligibility.API.Domain.ApplicationEvidence;
using Establishment = CheckYourEligibility.API.Domain.Establishment;

namespace CheckYourEligibility.API.Data.Mappings;

[ExcludeFromCodeCoverage]
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<IEligibilityServiceType, EligibilityCheck>()
            .ReverseMap();
        CreateMap<EligibilityCheck, CheckEligibilityItem>()
            .ReverseMap();
        CreateMap<ApplicationRequestData, Application>()
            .ForMember(dest => dest.ParentNationalInsuranceNumber,
                opt => opt.MapFrom(src => src.ParentNationalInsuranceNumber))
            .ForMember(dest => dest.ParentNationalAsylumSeekerServiceNumber,
                opt => opt.MapFrom(src => src.ParentNationalAsylumSeekerServiceNumber))
            .ForMember(x => x.ParentDateOfBirth,
                y => y.MapFrom(
                    z => DateTime.ParseExact(z.ParentDateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture)))
            .ForMember(x => x.ChildDateOfBirth,
                y => y.MapFrom(z =>
                    DateTime.ParseExact(z.ChildDateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture)))
            .ForMember(dest => dest.LocalAuthorityID, opt => opt.Ignore())
            .ForMember(dest => dest.EstablishmentId, opt => opt.MapFrom(src => src.Establishment))
            .ForMember(dest => dest.Establishment, opt => opt.Ignore())
            .ForMember(dest => dest.Evidence, opt => opt.Ignore())
            .ReverseMap();

        CreateMap<ApplicationEvidenceResponse, ApplicationEvidence>()
            .ForMember(dest => dest.ApplicationEvidenceID, opt => opt.Ignore())
            .ForMember(dest => dest.Application, opt => opt.Ignore())
            .ForMember(dest => dest.ApplicationID, opt => opt.Ignore())
            .ReverseMap();

        CreateMap<Application, ApplicationResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ApplicationID))
            .ForMember(dest => dest.ParentNationalInsuranceNumber,
                opt => opt.MapFrom(src => src.ParentNationalInsuranceNumber))
            .ForMember(dest => dest.ParentNationalAsylumSeekerServiceNumber,
                opt => opt.MapFrom(src => src.ParentNationalAsylumSeekerServiceNumber))
            .ForMember(x => x.ParentDateOfBirth, y => y.MapFrom(z => z.ParentDateOfBirth.ToString("yyyy-MM-dd")))
            .ForMember(x => x.ChildDateOfBirth, y => y.MapFrom(z => z.ChildDateOfBirth.ToString("yyyy-MM-dd")))
            .ForMember(x => x.Status, y => y.MapFrom(z => z.Status.ToString()))
            .ForMember(x => x.Evidence, y => y.MapFrom(z => z.Evidence))
            .ReverseMap();

        CreateMap<Establishment, ApplicationEstablishment>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.EstablishmentID))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.EstablishmentName))
            .ReverseMap();

        CreateMap<LocalAuthority, ApplicationEstablishment.EstablishmentLocalAuthority>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.LocalAuthorityID))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.LaName))
            .ReverseMap();


        CreateMap<FosterFamilyRequestData, FosterCarer>()
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.CarerFirstName))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.CarerLastName))
            .ForMember(dest => dest.DateOfBirth, opt => opt.MapFrom(src => src.CarerDateOfBirth))
            .ForMember(dest => dest.NationalInsuranceNumber, opt => opt.MapFrom(src => src.CarerNationalInsuranceNumber))

            .ForMember(dest => dest.HasPartner, opt => opt.MapFrom(src => src.HasPartner))
            .ForMember(dest => dest.PartnerFirstName, opt => opt.MapFrom(src => src.PartnerFirstName))
            .ForMember(dest => dest.PartnerLastName, opt => opt.MapFrom(src => src.PartnerLastName))
            .ForMember(dest => dest.PartnerDateOfBirth, opt => opt.MapFrom(src => src.PartnerDateOfBirth))
            .ForMember(dest => dest.PartnerNationalInsuranceNumber, opt => opt.MapFrom(src => src.PartnerNationalInsuranceNumber))

            .ForPath(dest => dest.FosterChild.FirstName, opt => opt.MapFrom(src => src.ChildFirstName))
            .ForPath(dest => dest.FosterChild.LastName, opt => opt.MapFrom(src => src.ChildLastName))
            .ForPath(dest => dest.FosterChild.DateOfBirth, opt => opt.MapFrom(src => src.ChildDateOfBirth))
            .ForPath(dest => dest.FosterChild.PostCode, opt => opt.MapFrom(src => src.ChildPostCode))

            .ForPath(dest => dest.FosterChild.SubmissionDate,
                opt => opt.MapFrom(src => src.SubmissionDate == default
                    ? DateTime.UtcNow
                    : src.SubmissionDate))

            .ForPath(dest => dest.FosterChild.ValidityStartDate,
                opt => opt.MapFrom(src => src.SubmissionDate == default
                    ? DateTime.UtcNow
                    : src.SubmissionDate))

            .ForPath(dest => dest.FosterChild.ValidityEndDate,
                opt => opt.MapFrom(src => (src.SubmissionDate == default
                    ? DateTime.UtcNow
                    : src.SubmissionDate).AddMonths(3)))

            .ForMember(dest => dest.Created, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.Updated, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForPath(dest => dest.FosterChild.Created, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForPath(dest => dest.FosterChild.Updated, opt => opt.MapFrom(src => DateTime.UtcNow))

            .ReverseMap();


        CreateMap<FosterFamilyUpdateRequest, FosterCarer>()
            .ForMember(d => d.FirstName, o => o.MapFrom(s => s.CarerFirstName))
            .ForMember(d => d.LastName, o => o.MapFrom(s => s.CarerLastName))
            .ForMember(d => d.DateOfBirth, o => o.MapFrom(s => s.CarerDateOfBirth))
            .ForMember(d => d.NationalInsuranceNumber, o => o.MapFrom(s => s.CarerNationalInsuranceNumber))

            .ForMember(d => d.HasPartner, o => o.MapFrom(s => s.HasPartner))
            .ForMember(d => d.PartnerFirstName, o => o.MapFrom(s => s.PartnerFirstName))
            .ForMember(d => d.PartnerLastName, o => o.MapFrom(s => s.PartnerLastName))
            .ForMember(d => d.PartnerDateOfBirth, o => o.MapFrom(s => s.PartnerDateOfBirth))
            .ForMember(d => d.PartnerNationalInsuranceNumber, o => o.MapFrom(s => s.PartnerNationalInsuranceNumber))
            .AfterMap((s, d) =>
            {
                if (s.HasPartner != true)
                {
                    d.PartnerFirstName = null;
                    d.PartnerLastName = null;
                    d.PartnerDateOfBirth = null;
                    d.PartnerNationalInsuranceNumber = null;
                }
            })


                .ForPath(d => d.FosterChild.FirstName, o =>
                {
                    o.Condition(ctx => ctx.Source.ChildFirstName != null);
                    o.MapFrom(src => src.ChildFirstName);
                })
                .ForPath(d => d.FosterChild.LastName, o =>
                {
                    o.Condition(ctx => ctx.Source.ChildLastName != null);
                    o.MapFrom(src => src.ChildLastName);
                })
                .ForPath(d => d.FosterChild.DateOfBirth, o =>
                {
                    o.Condition(ctx => ctx.Source.ChildDateOfBirth.HasValue);
                    o.MapFrom(src => src.ChildDateOfBirth);
                })
                .ForPath(d => d.FosterChild.PostCode, o =>
                {
                    o.Condition(ctx => ctx.Source.ChildPostCode != null);
                    o.MapFrom(src => src.ChildPostCode);
                })

            .ForPath(d => d.FosterChild.ValidityStartDate, o => o.Ignore())
            .ForPath(d => d.FosterChild.ValidityEndDate, o => o.Ignore())
            .ForPath(d => d.FosterChild.SubmissionDate, o => o.Ignore())
            .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));

        CreateMap<FosterFamilyUpdateRequest, WorkingFamiliesEvent>()
            .ForMember(wf => wf.ParentFirstName, o => o.MapFrom(s => s.CarerFirstName))
            .ForMember(wf => wf.ParentLastName, o => o.MapFrom(s => s.CarerLastName))
            .ForMember(wf => wf.PartnerNationalInsuranceNumber, o => o.MapFrom(s => s.CarerNationalInsuranceNumber))

            .ForMember(wf => wf.ChildFirstName, ff => ff.MapFrom(x => x.ChildFirstName))
            .ForMember(wf => wf.ChildLastName, ff => ff.MapFrom(x => x.ChildLastName))
            .ForMember(wf => wf.ChildPostCode, ff => ff.MapFrom(x => x.ChildPostCode))
            .ForMember(wf => wf.ChildDateOfBirth, ff => ff.MapFrom(x => x.ChildDateOfBirth))


            //.ForMember(wf => wf.HasPartner, o => o.MapFrom(s => s.HasPartner))
            .ForMember(wf => wf.PartnerFirstName, o => o.MapFrom(s => s.PartnerFirstName))
            .ForMember(wf => wf.PartnerLastName, o => o.MapFrom(s => s.PartnerLastName))
            .ForMember(wf => wf.PartnerNationalInsuranceNumber, o => o.MapFrom(s => s.PartnerNationalInsuranceNumber))


            .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));




        CreateMap<FosterCarer, FosterFamilyResponse>()
            .ForMember(dest => dest.FosterCarerId, opt => opt.MapFrom(src => src.FosterCarerId))
            .ForMember(dest => dest.CarerFirstName, opt => opt.MapFrom(src => src.FirstName))
            .ForMember(dest => dest.CarerLastName, opt => opt.MapFrom(src => src.LastName))
            .ForMember(dest => dest.CarerDateOfBirth, opt => opt.MapFrom(src => src.DateOfBirth))
            .ForMember(dest => dest.CarerNationalInsuranceNumber, opt => opt.MapFrom(src => src.NationalInsuranceNumber))

            .ForMember(dest => dest.HasPartner, opt => opt.MapFrom(src => src.HasPartner))
            .ForMember(dest => dest.PartnerFirstName, opt => opt.MapFrom(src => src.PartnerFirstName))
            .ForMember(dest => dest.PartnerLastName, opt => opt.MapFrom(src => src.PartnerLastName))
            .ForMember(dest => dest.PartnerDateOfBirth, opt => opt.MapFrom(src => src.PartnerDateOfBirth))
            .ForMember(dest => dest.PartnerNationalInsuranceNumber, opt => opt.MapFrom(src => src.PartnerNationalInsuranceNumber))

            .ForMember(dest => dest.ChildFirstName, opt => opt.MapFrom(src => src.FosterChild.FirstName))
            .ForMember(dest => dest.ChildLastName, opt => opt.MapFrom(src => src.FosterChild.LastName))
            .ForMember(dest => dest.ChildDateOfBirth, opt => opt.MapFrom(src => src.FosterChild.DateOfBirth))
            .ForMember(dest => dest.ChildPostCode, opt => opt.MapFrom(src => src.FosterChild.PostCode))

            .ForMember(dest => dest.SubmissionDate, opt => opt.MapFrom(src => src.FosterChild.SubmissionDate))
            .ForMember(dest => dest.ValidityStartDate, opt => opt.MapFrom(src => src.FosterChild.ValidityStartDate))
            .ForMember(dest => dest.ValidityEndDate, opt => opt.MapFrom(src => src.FosterChild.ValidityEndDate))
            .ReverseMap();


        CreateMap<User, UserData>()
            .ReverseMap();

        CreateMap<User, ApplicationUser>()
            .ReverseMap();

        CreateMap<Audit, AuditData>()
            .ReverseMap();

        CreateMap<EligibilityCheckHash, ApplicationHash>()
            .ReverseMap();
    }


}