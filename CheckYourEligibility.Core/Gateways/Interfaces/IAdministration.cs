using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Domain.CsvImport;
using CheckYourEligibility.Core.Gateways.CsvImport;

namespace CheckYourEligibility.Core.Gateways.Interfaces;

public interface IAdministration
{
    Task CleanUpEligibilityChecks();
    Task ImportEstablishments(IEnumerable<EstablishmentRow> data);
    Task ImportMats(IEnumerable<MatRow> data);
    Task ImportHMRCData(IEnumerable<FreeSchoolMealsHMRC> data);
    Task ImportHomeOfficeData(IEnumerable<FreeSchoolMealsHO> data);
    Task ImportWfHMRCData(IEnumerable<WorkingFamiliesEvent> data);
    Task UpdateEstablishmentsPrivateBeta(IEnumerable<EstablishmentPrivateBetaRow> data);
}