using System.Globalization;
using System.Text.RegularExpressions;
using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Domain.Constants;
using CheckYourEligibility.Core.Domain.CsvImport;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;

namespace CheckYourEligibility.Core.UseCases
{

    public record HomeOfficeImportResult(int RowsImported, int RowsWithoutFamilyName);

    public interface IImportFsmHomeOfficeDataUseCase
    {
        Task<HomeOfficeImportResult> Execute(IFormFile file);
    }

    public class ImportFsmHomeOfficeDataUseCase : IImportFsmHomeOfficeDataUseCase
    {
        private static readonly Regex AsylumSeekerReferenceNumberRegex =
            new(@"^(?:\d{9}|\d{2}/\d{2}/\d{5})$", RegexOptions.Compiled);

        private const int MaxReportedRowErrors = 20;
        private static readonly string[] RequiredColumns = ["AS Ref", "Port Ref", "Given Name", "Family Name", "DOB"];
        private static readonly string ExpectedColumns = string.Join(", ", RequiredColumns);

        private readonly IAudit _auditGateway;
        private readonly IAdministration _gateway;
        private readonly ILogger<ImportFsmHomeOfficeDataUseCase> _logger;

        public ImportFsmHomeOfficeDataUseCase(IAdministration Gateway, IAudit auditGateway,
            ILogger<ImportFsmHomeOfficeDataUseCase> logger)
        {
            _gateway = Gateway;
            _auditGateway = auditGateway;
            _logger = logger;
        }

        public async Task<HomeOfficeImportResult> Execute(IFormFile file)
        {
            if (file == null || file.ContentType.ToLower() != "text/csv")
                throw new InvalidDataException($"{Admin.CsvfileRequired}");

            List<FreeSchoolMealsHO> DataLoad = [];
            var rowErrors = new List<string>();
            var missingColumns = new List<string>();
            var currentRow = 0;
            try
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    BadDataFound = null,
                    PrepareHeaderForMatch = args => args.Header.Trim()
                };
                using (var fileStream = file.OpenReadStream())
                using (var csv = new CsvReader(new StreamReader(fileStream), config))
                {
                    csv.Context.RegisterClassMap<HomeOfficeRowMap>();
                    if (csv.Read())
                    {
                        csv.ReadHeader();
                        var presentColumns = csv.HeaderRecord.Select(h => h.Trim()).ToHashSet();
                        missingColumns = RequiredColumns.Where(c => !presentColumns.Contains(c)).ToList();
                        while (missingColumns.Count == 0 && csv.Read())
                        {
                            currentRow = csv.Context.Parser.Row;
                            var record = csv.GetRecord<HomeOfficeRow>();

                            var hasValidAsRef =
                                TryNormalizeAsylumSeekerReferenceNumber(record.AsylumSeekerReferenceNumber, out var nass);
                            var hasValidDob = TryParseDateOfBirth(record.Dob, out var dateOfBirth);
                            if (!hasValidAsRef)
                                rowErrors.Add($"Row {currentRow}: AS Ref must be nine digits, optionally formatted as 00/00/00000.");
                            if (!hasValidDob)
                                rowErrors.Add($"Row {currentRow}: DOB must be a valid date in the format dd/MM/yyyy, for example 31/01/1990.");
                            if (!hasValidAsRef || !hasValidDob) continue;

                            DataLoad.Add(new FreeSchoolMealsHO
                            {
                                FreeSchoolMealsHOID = Guid.NewGuid().ToString(),
                                FirstName = record.FirstName?.Trim(),
                                LastName = record.Surname?.Trim() ?? string.Empty,
                                DateOfBirth = dateOfBirth,
                                NASS = nass,
                                PortReferenceNumber = record.PortReferenceNumber?.Trim()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Deliberately not logging the exception message: CsvHelper messages can include row data.
                _logger.LogError("ImportHomeOfficeData failed at row {Row}: {ExceptionType}", currentRow,
                    ex.GetType().Name);
                throw new InvalidDataException(
                    $"{file.FileName}: the file could not be read near row {currentRow}. Check it is a CSV with the expected columns: {ExpectedColumns}.");
            }

            if (missingColumns.Count > 0)
                throw new InvalidDataException(
                    $"{file.FileName}: missing or misnamed column(s): {string.Join(", ", missingColumns)}. Expected columns: {ExpectedColumns}.");

            if (rowErrors.Count > 0)
                throw new InvalidDataException(FormatRowErrors(file.FileName, rowErrors));

            if (DataLoad.Count == 0)
                throw new InvalidDataException($"{file.FileName}: the file contains no data rows.");

            await _gateway.ImportHomeOfficeData(DataLoad);

            // Rows without a family name load fine but can never match, so they are reported back.
            return new HomeOfficeImportResult(DataLoad.Count, DataLoad.Count(x => x.LastName.Length == 0));
        }

        private static string FormatRowErrors(string fileName, List<string> errors)
        {
            var shown = string.Join(" ", errors.Take(MaxReportedRowErrors));
            var remaining = errors.Count > MaxReportedRowErrors
                ? $" ...and {errors.Count - MaxReportedRowErrors} more."
                : string.Empty;
            return $"{fileName}: {errors.Count} error(s) found, no data was imported. {shown}{remaining}";
        }

        private static bool TryParseDateOfBirth(string value, out DateTime dateOfBirth)
        {
            return DateTime.TryParseExact(value?.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out dateOfBirth);
        }

        private static bool TryNormalizeAsylumSeekerReferenceNumber(string value, out string normalized)
        {
            var trimmedValue = value?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedValue) ||
                !AsylumSeekerReferenceNumberRegex.IsMatch(trimmedValue))
            {
                normalized = string.Empty;
                return false;
            }

            normalized = trimmedValue.Replace("/", string.Empty);
            return true;
        }
    }
}