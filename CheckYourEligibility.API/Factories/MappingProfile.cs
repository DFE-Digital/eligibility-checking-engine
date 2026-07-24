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
            .ForMember(dest => dest.EligibilityCheckID, opt => opt.Ignore())
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
            .ForMember(x => x.Tier, y => y.MapFrom(z => z.Tier.ToString()))
            .ReverseMap();

        CreateMap<Establishment, ApplicationEstablishment>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.EstablishmentID))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.EstablishmentName))
            .ReverseMap();

        CreateMap<LocalAuthority, ApplicationEstablishment.EstablishmentLocalAuthority>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.LocalAuthorityID))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.LaName))
            .ReverseMap();


        CreateMap<WorkingFamiliesEvent, WorkingFamiliesEventEligibilityCodeRepsonseRecord>()
            .ForMember(dest => dest.EventId, opt => opt.MapFrom(src => src.WorkingFamiliesEventID.ToString()))
            .ForMember(dest => dest.SubmissionDate, opt => opt.MapFrom(src => src.SubmissionDate))
            .ForMember(dest => dest.DiscretionaryStartDate, opt => opt.MapFrom(src => src.DiscretionaryValidityStartDate))
            .ForMember(dest => dest.ValidityStartDate, opt => opt.MapFrom(src => src.ValidityStartDate))
            .ForMember(dest => dest.ValidityEndDate, opt => opt.MapFrom(src => src.ValidityEndDate))
            .ForMember(dest => dest.GracePeriodEndDate, opt => opt.MapFrom(src => src.GracePeriodEndDate))
            .ForMember(dest => dest.ParentNationalInsuranceNumber,
                opt => opt.MapFrom(src => src.ParentNationalInsuranceNumber))
            .ForMember(dest => dest.ParentFirstName, opt => opt.MapFrom(src => src.ParentFirstName))
            .ForMember(dest => dest.ParentLastName, opt => opt.MapFrom(src => src.ParentLastName))
            .ForMember(dest => dest.ParentDateOfBirth,
                opt => opt.MapFrom(src => src.ParentDateOfBirth))
            .ForMember(dest => dest.PartnerNationalInsuranceNumber,
                opt => opt.MapFrom(src => src.PartnerNationalInsuranceNumber))
            .ForMember(dest => dest.PartnerFirstName, opt => opt.MapFrom(src => src.PartnerFirstName))
            .ForMember(dest => dest.PartnerLastName, opt => opt.MapFrom(src => src.PartnerLastName))
            .ForMember(dest => dest.PartnerDateOfBirth,
                opt => opt.MapFrom(src => src.PartnerDateOfBirth))
            .ForMember(dest => dest.ChildFirstName, opt =>
                opt.MapFrom(src =>
                    src.ChildFirstName)) 
            .ForMember(dest => dest.ChildLastName, opt =>
                opt.MapFrom(src =>
                    src.ChildLastName)) 
            .ForMember(dest => dest.ChildDateOfBirth,
                opt =>
                    opt.MapFrom(src =>
                        src.ChildDateOfBirth)) 
            .ForMember(dest => dest.ChildPostCode, opt =>
                opt.MapFrom(src =>
                    src.ChildPostCode))
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