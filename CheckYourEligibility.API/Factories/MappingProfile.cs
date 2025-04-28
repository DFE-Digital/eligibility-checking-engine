using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using static CheckYourEligibility.API.Boundary.Responses.ApplicationResponse;
using Establishment = CheckYourEligibility.API.Domain.Establishment;

namespace CheckYourEligibility.API.Data.Mappings;

[ExcludeFromCodeCoverage]
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<CheckEligibilityRequestData_Fsm, EligibilityCheck>()
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
            .ForMember(dest => dest.LocalAuthorityId, opt => opt.Ignore())
            .ForMember(dest => dest.EstablishmentId, opt => opt.MapFrom(src => src.Establishment))
            .ForMember(dest => dest.Establishment, opt => opt.Ignore())
            .ForMember(dest => dest.Evidence, opt => opt.Ignore())
            .ReverseMap();
        
        CreateMap<Boundary.Shared.ApplicationEvidence, ApplicationEvidence>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
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
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.EstablishmentId))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.EstablishmentName))
            .ReverseMap();

        CreateMap<LocalAuthority, ApplicationEstablishment.EstablishmentLocalAuthority>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.LocalAuthorityId))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.LaName))
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