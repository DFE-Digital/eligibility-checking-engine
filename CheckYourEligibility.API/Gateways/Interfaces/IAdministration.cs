// Ignore Spelling: Fsm

using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways.CsvImport;

namespace CheckYourEligibility.API.Gateways.Interfaces;

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