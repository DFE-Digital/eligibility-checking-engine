using System.Globalization;
using System.Xml.Linq;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.UseCases;

public interface IImportFsmHMRCDataUseCase
{
    Task Execute(IFormFile file);
}

public class ImportFsmHMRCDataUseCase : IImportFsmHMRCDataUseCase
{
    private readonly IAudit _auditGateway;
    private readonly IAdministration _gateway;
    private readonly ILogger<ImportFsmHMRCDataUseCase> _logger;

    public ImportFsmHMRCDataUseCase(IAdministration Gateway, IAudit auditGateway,
        ILogger<ImportFsmHMRCDataUseCase> logger)
    {
        _gateway = Gateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task Execute(IFormFile file)
    {
        List<FreeSchoolMealsHMRC> DataLoad = new();
        if (file == null || file.ContentType.ToLower() != "text/xml")
            throw new InvalidDataException($"{Admin.XmlfileRequired}");
        try
        {
            using var fileStream = file.OpenReadStream();
            var po = XElement.Load(fileStream);
            IEnumerable<XElement> childElements =
                from el in po.Elements()
                select el;
            var EligiblePersons = childElements.FirstOrDefault(x => x.Name.LocalName == "EligiblePersons");
            if (EligiblePersons != null)
                DataLoad.AddRange(from XElement EligiblePerson in EligiblePersons.Nodes()
                    let elements = EligiblePerson.Elements()
                    let item = new FreeSchoolMealsHMRC
                    {
                        FreeSchoolMealsHMRCID = elements.First(x => x.Name.LocalName == "NINO").Value,
                        DataType = Convert.ToInt32(elements.First(x => x.Name.LocalName == "DataType").Value),
                        Surname = elements.First(x => x.Name.LocalName == "Surname").Value,
                        DateOfBirth = DateTime.ParseExact(elements.First(x => x.Name.LocalName == "DateOfBirth").Value,
                            "ddMMyyyy", CultureInfo.InvariantCulture)
                    }
                    select item);

            if (DataLoad == null || DataLoad.Count == 0) throw new InvalidDataException("Invalid file no content.");
        }
        catch (Exception ex)
        {
            _logger.LogError("ImportHMRCData", ex);
            throw new InvalidDataException(
                $"{file.FileName} - {JsonConvert.SerializeObject(new FreeSchoolMealsHMRC())} :- {ex.Message}, {ex.InnerException?.Message}");
        }

        await _gateway.ImportHMRCData(DataLoad);
    }
}