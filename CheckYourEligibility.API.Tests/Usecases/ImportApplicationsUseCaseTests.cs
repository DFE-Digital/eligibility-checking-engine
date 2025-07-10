using AutoFixture;
using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using DomainEstablishment = CheckYourEligibility.API.Domain.Establishment;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class ImportApplicationsUseCaseTests : TestBase.TestBase
{
    private Mock<IApplication> _mockApplicationGateway = null!;
    private Mock<IAudit> _mockAuditGateway = null!;
    private Mock<ILogger<ImportApplicationsUseCase>> _mockLogger = null!;
    private Mock<IMapper> _mockMapper = null!;
    private ImportApplicationsUseCase _sut = null!;
    private new Fixture _fixture = null!;

    [SetUp]
    public void Setup()
    {
        _mockApplicationGateway = new Mock<IApplication>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<ImportApplicationsUseCase>>(MockBehavior.Loose);
        _mockMapper = new Mock<IMapper>(MockBehavior.Strict);
        _fixture = new Fixture();

        _sut = new ImportApplicationsUseCase(
            _mockApplicationGateway.Object,
            _mockAuditGateway.Object,
            _mockLogger.Object,
            _mockMapper.Object);
    }
    [TearDown]
    public void Teardown()
    {
        _mockApplicationGateway.VerifyAll();
        // Don't verify audit gateway as it may not be called in all test cases
    }
    [Test]
    public async Task Execute_Should_Return_Error_When_File_Is_Null()
    {
        // Arrange
        var request = new ApplicationBulkImportRequest { File = null! };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Import failed - file is required.");
        result.Errors.Should().Contain("File required.");
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(0);
        result.TotalRecords.Should().Be(0);
    }
    [Test]
    [TestCase("application/pdf")]
    [TestCase("text/plain")]
    [TestCase("application/xml")]
    public async Task Execute_Should_Return_Error_When_File_Type_Is_Invalid(string contentType)
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Import failed - CSV or JSON file is required.");
        result.Errors.Should().Contain("CSV or JSON file required.");
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(0);
        result.TotalRecords.Should().Be(0);
    }
    [Test]
    public async Task Execute_Should_Process_Valid_CSV_File()
    {
        // Arrange
        var csvContent = "Parent First Name,Parent Surname,Parent DOB,Parent Nino,Parent Email Address,Child First Name,Child Surname,Child Date of Birth,Child School URN,Eligibility End Date\n" +
                        "John,Smith,1985-03-15,AB123456C,john.smith@example.com,Emma,Smith,2015-04-12,123456,2025-07-31";

        var fileMock = CreateMockFile(csvContent, "text/csv");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 }; var establishment = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1,
            EstablishmentName = "Test School"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment }
        };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(1);
        result.FailedImports.Should().Be(0);
        result.TotalRecords.Should().Be(1);
        result.Message.Should().Contain("Import completed successfully");
    }
    [Test]
    public async Task Execute_Should_Process_Valid_JSON_File()
    {        // Arrange
        var jsonContent = "[{" +
                         "\"ParentFirstName\": \"John\"," +
                         "\"ParentSurname\": \"Smith\"," +
                         "\"ParentDateOfBirth\": \"1985-03-15\"," +
                         "\"ParentNino\": \"AB123456C\"," +
                         "\"ParentEmail\": \"john.smith@example.com\"," +
                         "\"ChildFirstName\": \"Emma\"," +
                         "\"ChildSurname\": \"Smith\"," +
                         "\"ChildDateOfBirth\": \"2015-04-12\"," +
                         "\"ChildSchoolUrn\": \"123456\"," +
                         "\"EligibilityEndDate\": \"2025-07-31\"" +
                         "}]";

        var fileMock = CreateMockFile(jsonContent, "application/json");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        var establishment = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1,
            EstablishmentName = "Test School"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment }
        };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(1);
        result.FailedImports.Should().Be(0);
        result.TotalRecords.Should().Be(1);
        result.Message.Should().Contain("Import completed successfully");
    }
    [Test]
    public async Task Execute_Should_Handle_Empty_CSV_File()
    {        // Arrange
        var csvContent = "Parent First Name,Parent Surname,Parent DOB,Parent Nino,Parent Email Address,Child First Name,Child Surname,Child Date of Birth,Child School URN,Eligibility End Date\n";

        var fileMock = CreateMockFile(csvContent, "text/csv");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Import failed - no valid records found in the file.");
        result.Errors.Should().Contain("Invalid file content - no records found.");
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(0);
        result.TotalRecords.Should().Be(0);
    }
    [Test]
    public async Task Execute_Should_Handle_Empty_JSON_Array()
    {
        // Arrange
        var jsonContent = "[]";

        var fileMock = CreateMockFile(jsonContent, "application/json");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Import failed - no valid records found in the file.");
        result.Errors.Should().Contain("Invalid file content - no records found.");
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(0);
        result.TotalRecords.Should().Be(0);
    }
    [Test]
    public async Task Execute_Should_Skip_Unauthorized_LocalAuthority_Applications()
    {
        // Arrange
        var csvContent = "Parent First Name,Parent Surname,Parent DOB,Parent Nino,Parent Email Address,Child First Name,Child Surname,Child Date of Birth,Child School URN,Eligibility End Date\n" +
                        "John,Smith,1985-03-15,AB123456C,john.smith@example.com,Emma,Smith,2015-04-12,123456,2025-07-31\n" +
                        "Jane,Doe,1990-02-20,CD789012E,jane.doe@example.com,Peter,Doe,2016-09-08,654321,2025-07-31";

        var fileMock = CreateMockFile(csvContent, "text/csv");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1 }; // Only LA 1 is allowed

        var establishment1 = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1, // Allowed
            EstablishmentName = "Test School 1"
        };

        var establishment2 = new DomainEstablishment
        {
            EstablishmentId = 654321,
            LocalAuthorityId = 2, // Not allowed
            EstablishmentName = "Test School 2"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment1 },
            { "654321", establishment2 }
        };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(1); // Only the authorized one
        result.FailedImports.Should().Be(1); // The unauthorized one
        result.TotalRecords.Should().Be(2);
        result.Errors.Should().Contain("Row 2: You do not have permission to create applications for this establishment's local authority");
    }
    [Test]
    public async Task Execute_Should_Allow_All_Applications_When_SuperUser()
    {
        // Arrange
        var csvContent = "Parent First Name,Parent Surname,Parent DOB,Parent Nino,Parent Email Address,Child First Name,Child Surname,Child Date of Birth,Child School URN,Eligibility End Date\n" +
                        "John,Smith,1985-03-15,AB123456C,john.smith@example.com,Emma,Smith,2015-04-12,123456,2025-07-31\n" +
                        "Jane,Doe,1990-02-20,CD789012E,jane.doe@example.com,Peter,Doe,2016-09-08,654321,2025-07-31";

        var fileMock = CreateMockFile(csvContent, "text/csv");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 0 }; // Super user (contains 0)

        var establishment1 = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1,
            EstablishmentName = "Test School 1"
        };

        var establishment2 = new DomainEstablishment
        {
            EstablishmentId = 654321,
            LocalAuthorityId = 2,
            EstablishmentName = "Test School 2"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment1 },
            { "654321", establishment2 }
        };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.Is<IEnumerable<Application>>(apps => apps.Count() == 2)))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(2);
        result.FailedImports.Should().Be(0);
        result.TotalRecords.Should().Be(2);
        result.Message.Should().Contain("Import completed successfully");
    }
    [Test]
    public async Task Execute_Should_Allow_Multiple_LocalAuthorities()
    {        // Arrange
        var csvContent = "Parent First Name,Parent Surname,Parent DOB,Parent Nino,Parent Email Address,Child First Name,Child Surname,Child Date of Birth,Child School URN,Eligibility End Date\n" +
                        "John,Smith,1985-03-15,AB123456C,john.smith@example.com,Emma,Smith,2015-04-12,123456,2025-07-31\n" +
                        "Jane,Doe,1990-02-20,CD789012E,jane.doe@example.com,Peter,Doe,2016-09-08,654321,2025-07-31\n" +
                        "Bob,Wilson,1988-05-10,EF345678G,bob.wilson@example.com,Alice,Wilson,2014-12-20,789012,2025-07-31";

        var fileMock = CreateMockFile(csvContent, "text/csv");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1, 2 }; // LA 1 and 2 are allowed, but not 3

        var establishment1 = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1, // Allowed
            EstablishmentName = "Test School 1"
        };

        var establishment2 = new DomainEstablishment
        {
            EstablishmentId = 654321,
            LocalAuthorityId = 2, // Allowed
            EstablishmentName = "Test School 2"
        };

        var establishment3 = new DomainEstablishment
        {
            EstablishmentId = 789012,
            LocalAuthorityId = 3, // Not allowed
            EstablishmentName = "Test School 3"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment1 },
            { "654321", establishment2 },
            { "789012", establishment3 }
        };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(2); // First two should be allowed
        result.FailedImports.Should().Be(1); // Third should be rejected
        result.TotalRecords.Should().Be(3);
        result.Errors.Should().Contain("Row 3: You do not have permission to create applications for this establishment's local authority");
    }
    [Test]
    public async Task Execute_Should_Handle_Mixed_Authorization_And_Other_Errors()
    {
        // Arrange
        var csvContent = "Parent First Name,Parent Surname,Parent DOB,Parent Nino,Parent Email Address,Child First Name,Child Surname,Child Date of Birth,Child School URN,Eligibility End Date\n" +
                        "John,Smith,1985-03-15,AB123456C,john.smith@example.com,Emma,Smith,2015-04-12,123456,2025-07-31\n" +
                        "Jane,Doe,1990-02-20,CD789012E,jane.doe@example.com,Peter,Doe,2016-09-08,999999,2025-07-31\n" +
                        "Bob,Wilson,1988-05-10,EF345678G,bob.wilson@example.com,Alice,Wilson,2014-12-20,654321,2025-07-31";

        var fileMock = CreateMockFile(csvContent, "text/csv");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1 }; // Only LA 1 is allowed

        var establishment1 = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1, // Allowed
            EstablishmentName = "Test School 1"
        };

        var establishment3 = new DomainEstablishment
        {
            EstablishmentId = 654321,
            LocalAuthorityId = 2, // Not allowed
            EstablishmentName = "Test School 3"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment1 },
            { "654321", establishment3 }
            // Note: establishment 999999 is missing - this will cause an "establishment not found" error
        };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(1); // Only the first one should succeed
        result.FailedImports.Should().Be(2); // Both others should fail
        result.TotalRecords.Should().Be(3);
        result.Errors.Should().Contain("Row 2: Establishment with URN 999999 not found");
        result.Errors.Should().Contain("Row 3: You do not have permission to create applications for this establishment's local authority");
    }
    [Test]
    public async Task Execute_Should_Handle_Invalid_CSV_Data()
    {
        // Arrange
        var csvContent = "Parent First Name,Parent Surname,Parent DOB,Parent Nino,Parent Email Address,Child First Name,Child Surname,Child Date of Birth,Child School URN,Eligibility End Date\n" +
                        ",Smith,1985-03-15,AB123456C,john.smith@example.com,Emma,Smith,2015-04-12,123456,2025-07-31\n" +
                        "Jane,,1990-02-20,CD789012E,jane.doe@example.com,Peter,Doe,2016-09-08,654321,2025-07-31\n" +
                        "Bob,Wilson,invalid-date,EF345678G,bob.wilson@example.com,Alice,Wilson,2014-12-20,789012,2025-07-31";

        var fileMock = CreateMockFile(csvContent, "text/csv");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        // Setup mock for URN lookup
        var establishmentLookup = new Dictionary<string, DomainEstablishment>();
        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);

        // Setup audit mock - won't actually be called, but need to set it up for teardown
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Override verification for this test
        _mockAuditGateway.Verify(x => x.CreateAuditEntry(It.IsAny<AuditType>(), It.IsAny<string>()), Times.Never);

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(3);
        result.TotalRecords.Should().Be(3);
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().Contain(error => error.Contains("Row 1:") && error.Contains("Parent first name"));
        result.Errors.Should().Contain(error => error.Contains("Row 2:") && error.Contains("Parent surname"));
        result.Errors.Should().Contain(error => error.Contains("Row 3:") && error.Contains("Parent date of birth"));
    }
    [Test]
    public async Task Execute_Should_Handle_Invalid_JSON_Data()
    {
        // Arrange
        var jsonContent = "[{" +
                         "\"ParentFirstName\": \"\"," +
                         "\"ParentSurname\": \"Smith\"," +
                         "\"ParentDateOfBirth\": \"1985-03-15\"," +
                         "\"ParentNino\": \"AB123456C\"," +
                         "\"ParentEmail\": \"john.smith@example.com\"," +
                         "\"ChildFirstName\": \"Emma\"," +
                         "\"ChildSurname\": \"Smith\"," +
                         "\"ChildDateOfBirth\": \"2015-04-12\"," +
                         "\"ChildSchoolUrn\": \"123456\"," +
                         "\"EligibilityEndDate\": \"2025-07-31\"" +
                         "}]";

        var fileMock = CreateMockFile(jsonContent, "application/json");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        // Setup mock for URN lookup
        var establishmentLookup = new Dictionary<string, DomainEstablishment>();
        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);

        // Setup audit mock - won't actually be called, but need to set it up for teardown
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Override verification for this test
        _mockAuditGateway.Verify(x => x.CreateAuditEntry(It.IsAny<AuditType>(), It.IsAny<string>()), Times.Never);

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(1);
        result.TotalRecords.Should().Be(1);
        result.Errors.Should().Contain(error => error.Contains("Row 1:") && error.Contains("Parent first name"));
    }
    [Test]
    public async Task Execute_Should_Handle_Malformed_CSV_File()
    {
        // Arrange
        var csvContent = "Malformed CSV content without proper headers\n" +
                        "Some,random,data";

        var fileMock = CreateMockFile(csvContent, "text/csv");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Contain("Error parsing CSV file");
        result.Errors.Should().Contain(error => error.Contains("Error parsing CSV file"));
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(0);
        result.TotalRecords.Should().Be(0);
    }
    [Test]
    public async Task Execute_Should_Handle_Malformed_JSON_File()
    {
        // Arrange
        var jsonContent = "{ invalid json content";

        var fileMock = CreateMockFile(jsonContent, "application/json");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Contain("Error parsing JSON file");
        result.Errors.Should().Contain(error => error.Contains("Error parsing JSON file"));
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(0);
        result.TotalRecords.Should().Be(0);
    }
    [Test]
    public async Task Execute_Should_Handle_BulkImport_Gateway_Exception()
    {
        // Arrange
        var csvContent = "Parent First Name,Parent Surname,Parent DOB,Parent Nino,Parent Email Address,Child First Name,Child Surname,Child Date of Birth,Child School URN,Eligibility End Date\n" +
                        "John,Smith,1985-03-15,AB123456C,john.smith@example.com,Emma,Smith,2015-04-12,123456,2025-07-31";

        var fileMock = CreateMockFile(csvContent, "text/csv");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        var establishment = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1,
            EstablishmentName = "Test School"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment }
        }; _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .ThrowsAsync(new Exception("Database error"));
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Don't verify audit as it won't be called on exception
        _mockAuditGateway.Verify(x => x.CreateAuditEntry(AuditType.Administration, string.Empty), Times.Never);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Import failed - error during bulk database operation.");
        result.Errors.Should().Contain("Error during bulk import: Database error");
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(1);
    }
    [Test]
    public void Execute_Should_Handle_GetEstablishments_Gateway_Exception() // Removed async
    {
        // Arrange
        var csvContent = "Parent First Name,Parent Surname,Parent DOB,Parent Nino,Parent Email Address,Child First Name,Child Surname,Child Date of Birth,Child School URN,Eligibility End Date\n" +
                        "John,Smith,1985-03-15,AB123456C,john.smith@example.com,Emma,Smith,2015-04-12,123456,2025-07-31";

        var fileMock = CreateMockFile(csvContent, "text/csv");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ThrowsAsync(new Exception("Database connection error"));

        // Act & Assert - The exception should bubble up
        var exception = Assert.ThrowsAsync<Exception>(async () => await _sut.Execute(request, allowedLocalAuthorityIds));

        exception.Should().NotBeNull();
        exception!.Message.Should().Be("Database connection error");
    }
    [Test]
    public async Task Execute_Should_Handle_Partial_Success_Scenario()
    {
        // Arrange
        var csvContent = "Parent First Name,Parent Surname,Parent DOB,Parent Nino,Parent Email Address,Child First Name,Child Surname,Child Date of Birth,Child School URN,Eligibility End Date\n" +
                        "John,Smith,1985-03-15,AB123456C,john.smith@example.com,Emma,Smith,2015-04-12,123456,2025-07-31\n" +
                        ",Doe,1990-02-20,CD789012E,jane.doe@example.com,Peter,Doe,2016-09-08,654321,2025-07-31\n" +
                        "Bob,Wilson,1988-05-10,EF345678G,bob.wilson@example.com,Alice,Wilson,2014-12-20,789012,2025-07-31";

        var fileMock = CreateMockFile(csvContent, "text/csv");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        var establishment1 = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1,
            EstablishmentName = "Test School 1"
        };

        var establishment3 = new DomainEstablishment
        {
            EstablishmentId = 789012,
            LocalAuthorityId = 3,
            EstablishmentName = "Test School 3"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment1 },
            { "789012", establishment3 }
        };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(2);
        result.FailedImports.Should().Be(1);
        result.TotalRecords.Should().Be(3);
        result.Message.Should().Contain("Import partially completed");
        result.Errors.Should().Contain(error => error.Contains("Row 2:") && error.Contains("Parent first name"));
    }
    [Test]
    [TestCase("text/csv")]
    [TestCase("application/csv")]
    [TestCase("application/json")]
    [TestCase("text/json")]
    public async Task Execute_Should_Accept_Valid_Content_Types(string contentType)
    {
        // Arrange
        string content;
        if (contentType.Contains("json"))
        {
            content = "[{" +
                     "\"ParentFirstName\": \"John\"," +
                     "\"ParentSurname\": \"Smith\"," +
                     "\"ParentDateOfBirth\": \"1985-03-15\"," +
                     "\"ParentNino\": \"AB123456C\"," +
                     "\"ParentEmail\": \"john.smith@example.com\"," +
                     "\"ChildFirstName\": \"Emma\"," +
                     "\"ChildSurname\": \"Smith\"," +
                     "\"ChildDateOfBirth\": \"2015-04-12\"," +
                     "\"ChildSchoolUrn\": \"123456\"," +
                     "\"EligibilityEndDate\": \"2025-07-31\"" +
                     "}]";
        }
        else
        {
            content = "Parent First Name,Parent Surname,Parent DOB,Parent Nino,Parent Email Address,Child First Name,Child Surname,Child Date of Birth,Child School URN,Eligibility End Date\n" +
                     "John,Smith,1985-03-15,AB123456C,john.smith@example.com,Emma,Smith,2015-04-12,123456,2025-07-31";
        }

        var fileMock = CreateMockFile(content, contentType);
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        var establishment = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1,
            EstablishmentName = "Test School"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment }
        };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(1);
        result.FailedImports.Should().Be(0);
        result.TotalRecords.Should().Be(1);
    }
    [Test]
    public async Task Execute_Should_Create_Applications_With_Correct_Properties()
    {
        // Arrange
        var csvContent = "Parent First Name,Parent Surname,Parent DOB,Parent Nino,Parent Email Address,Child First Name,Child Surname,Child Date of Birth,Child School URN,Eligibility End Date\n" +
                        "John,Smith,1985-03-15,AB123456C,john.smith@example.com,Emma,Smith,2015-04-12,123456,2025-07-31";

        var fileMock = CreateMockFile(csvContent, "text/csv");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        var establishment = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1,
            EstablishmentName = "Test School"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment }
        };

        Application capturedApplication = null!;

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .Callback<IEnumerable<Application>>(apps => capturedApplication = apps.First())
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(1);

        capturedApplication.Should().NotBeNull();
        capturedApplication.ParentFirstName.Should().Be("John");
        capturedApplication.ParentLastName.Should().Be("Smith");
        capturedApplication.ParentDateOfBirth.Should().Be(new DateTime(1985, 3, 15));
        capturedApplication.ParentNationalInsuranceNumber.Should().Be("AB123456C");
        capturedApplication.ParentEmail.Should().Be("john.smith@example.com");
        capturedApplication.ChildFirstName.Should().Be("Emma");
        capturedApplication.ChildLastName.Should().Be("Smith");
        capturedApplication.ChildDateOfBirth.Should().Be(new DateTime(2015, 4, 12));
        capturedApplication.EstablishmentId.Should().Be(123456);
        capturedApplication.LocalAuthorityId.Should().Be(1);
        capturedApplication.Type.Should().Be(CheckEligibilityType.FreeSchoolMeals);
        capturedApplication.Status.Should().Be(Domain.Enums.ApplicationStatus.SentForReview);
        capturedApplication.ApplicationID.Should().NotBeNullOrEmpty();
        capturedApplication.Reference.Should().NotBeNullOrEmpty();
        capturedApplication.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        capturedApplication.Updated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    private Mock<IFormFile> CreateMockFile(string content, string contentType)
    {
        var fileMock = new Mock<IFormFile>();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.Length).Returns(stream.Length);

        return fileMock;
    }

    #region ExecuteFromJson Tests


    [Test]
    public async Task ExecuteFromJson_Should_Return_Error_When_Applications_Is_Null()
    {
        // Arrange
        var request = new ApplicationBulkImportJsonRequest { Applications = null! };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Import failed - no application data provided.");
        result.Errors.Should().Contain("Application data required.");
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(0);
        result.TotalRecords.Should().Be(0);
    }


    [Test]
    public async Task ExecuteFromJson_Should_Return_Error_When_Applications_Is_Empty()
    {
        // Arrange
        var request = new ApplicationBulkImportJsonRequest { Applications = new List<ApplicationBulkImportData>() };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Import failed - no application data provided.");
        result.Errors.Should().Contain("Application data required.");
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(0);
        result.TotalRecords.Should().Be(0);
    }
    [Test]
    public async Task ExecuteFromJson_Should_Process_Valid_Single_Application()
    {
        // Arrange
        var applicationData = new ApplicationBulkImportData
        {
            ParentFirstName = "John",
            ParentSurname = "Smith",
            ParentDateOfBirth = "1985-03-15",
            ParentNino = "AB123456C",
            ParentEmail = "john.smith@example.com",
            ChildFirstName = "Emma",
            ChildSurname = "Smith",
            ChildDateOfBirth = "2015-04-12",
            ChildSchoolUrn = "123456",
            EligibilityEndDate = "2025-07-31"
        };

        var request = new ApplicationBulkImportJsonRequest { Applications = new List<ApplicationBulkImportData> { applicationData } };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        var establishment = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1,
            EstablishmentName = "Test School"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment }
        };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(1);
        result.FailedImports.Should().Be(0);
        result.TotalRecords.Should().Be(1);
        result.Message.Should().Contain("Import completed successfully");
    }
    [Test]
    public async Task ExecuteFromJson_Should_Process_Valid_Multiple_Applications()
    {
        // Arrange
        var applicationData1 = new ApplicationBulkImportData
        {
            ParentFirstName = "John",
            ParentSurname = "Smith",
            ParentDateOfBirth = "1985-03-15",
            ParentNino = "AB123456C",
            ParentEmail = "john.smith@example.com",
            ChildFirstName = "Emma",
            ChildSurname = "Smith",
            ChildDateOfBirth = "2015-04-12",
            ChildSchoolUrn = "123456",
            EligibilityEndDate = "2025-07-31"
        };

        var applicationData2 = new ApplicationBulkImportData
        {
            ParentFirstName = "Jane",
            ParentSurname = "Doe",
            ParentDateOfBirth = "1990-02-20",
            ParentNino = "CD789012E",
            ParentEmail = "jane.doe@example.com",
            ChildFirstName = "Peter",
            ChildSurname = "Doe",
            ChildDateOfBirth = "2016-09-08",
            ChildSchoolUrn = "654321",
            EligibilityEndDate = "2025-07-31"
        };

        var request = new ApplicationBulkImportJsonRequest { Applications = new List<ApplicationBulkImportData> { applicationData1, applicationData2 } };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        var establishment1 = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1,
            EstablishmentName = "Test School 1"
        };

        var establishment2 = new DomainEstablishment
        {
            EstablishmentId = 654321,
            LocalAuthorityId = 2,
            EstablishmentName = "Test School 2"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment1 },
            { "654321", establishment2 }
        };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(2);
        result.FailedImports.Should().Be(0);
        result.TotalRecords.Should().Be(2);
        result.Message.Should().Contain("Import completed successfully");
    }
    [Test]
    public async Task ExecuteFromJson_Should_Skip_Unauthorized_LocalAuthority_Applications()
    {
        // Arrange
        var applicationData1 = new ApplicationBulkImportData
        {
            ParentFirstName = "John",
            ParentSurname = "Smith",
            ParentDateOfBirth = "1985-03-15",
            ParentNino = "AB123456C",
            ParentEmail = "john.smith@example.com",
            ChildFirstName = "Emma",
            ChildSurname = "Smith",
            ChildDateOfBirth = "2015-04-12",
            ChildSchoolUrn = "123456",
            EligibilityEndDate = "2025-07-31"
        };

        var applicationData2 = new ApplicationBulkImportData
        {
            ParentFirstName = "Jane",
            ParentSurname = "Doe",
            ParentDateOfBirth = "1990-02-20",
            ParentNino = "CD789012E",
            ParentEmail = "jane.doe@example.com",
            ChildFirstName = "Peter",
            ChildSurname = "Doe",
            ChildDateOfBirth = "2016-09-08",
            ChildSchoolUrn = "654321",
            EligibilityEndDate = "2025-07-31"
        };

        var request = new ApplicationBulkImportJsonRequest { Applications = new List<ApplicationBulkImportData> { applicationData1, applicationData2 } };
        var allowedLocalAuthorityIds = new List<int> { 1 }; // Only LA 1 is allowed

        var establishment1 = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1, // Allowed
            EstablishmentName = "Test School 1"
        };

        var establishment2 = new DomainEstablishment
        {
            EstablishmentId = 654321,
            LocalAuthorityId = 2, // Not allowed
            EstablishmentName = "Test School 2"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment1 },
            { "654321", establishment2 }
        };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(1); // Only the authorized one
        result.FailedImports.Should().Be(1); // The unauthorized one
        result.TotalRecords.Should().Be(2);
        result.Errors.Should().Contain("Row 2: You do not have permission to create applications for this establishment's local authority");
    }


    [Test]
    public async Task ExecuteFromJson_Should_Allow_All_Applications_When_SuperUser()
    {
        // Arrange
        var applicationData1 = new ApplicationBulkImportData
        {
            ParentFirstName = "John",
            ParentSurname = "Smith",
            ParentDateOfBirth = "1985-03-15",
            ParentNino = "AB123456C",
            ParentEmail = "john.smith@example.com",
            ChildFirstName = "Emma",
            ChildSurname = "Smith",
            ChildDateOfBirth = "2015-04-12",
            ChildSchoolUrn = "123456",
            EligibilityEndDate = "2025-07-31"
        };

        var applicationData2 = new ApplicationBulkImportData
        {
            ParentFirstName = "Jane",
            ParentSurname = "Doe",
            ParentDateOfBirth = "1990-02-20",
            ParentNino = "CD789012E",
            ParentEmail = "jane.doe@example.com",
            ChildFirstName = "Peter",
            ChildSurname = "Doe",
            ChildDateOfBirth = "2016-09-08",
            ChildSchoolUrn = "654321",
            EligibilityEndDate = "2025-07-31"
        };

        var request = new ApplicationBulkImportJsonRequest { Applications = new List<ApplicationBulkImportData> { applicationData1, applicationData2 } };
        var allowedLocalAuthorityIds = new List<int> { 0 }; // Super user (contains 0)

        var establishment1 = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1,
            EstablishmentName = "Test School 1"
        };

        var establishment2 = new DomainEstablishment
        {
            EstablishmentId = 654321,
            LocalAuthorityId = 2,
            EstablishmentName = "Test School 2"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment1 },
            { "654321", establishment2 }
        };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.Is<IEnumerable<Application>>(apps => apps.Count() == 2)))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(2);
        result.FailedImports.Should().Be(0);
        result.TotalRecords.Should().Be(2);
        result.Message.Should().Contain("Import completed successfully");
    }
    [Test]
    public async Task ExecuteFromJson_Should_Allow_Multiple_LocalAuthorities()
    {
        // Arrange
        var applicationData1 = new ApplicationBulkImportData
        {
            ParentFirstName = "John",
            ParentSurname = "Smith",
            ParentDateOfBirth = "1985-03-15",
            ParentNino = "AB123456C",
            ParentEmail = "john.smith@example.com",
            ChildFirstName = "Emma",
            ChildSurname = "Smith",
            ChildDateOfBirth = "2015-04-12",
            ChildSchoolUrn = "123456",
            EligibilityEndDate = "2025-07-31"
        };

        var applicationData2 = new ApplicationBulkImportData
        {
            ParentFirstName = "Jane",
            ParentSurname = "Doe",
            ParentDateOfBirth = "1990-02-20",
            ParentNino = "CD789012E",
            ParentEmail = "jane.doe@example.com",
            ChildFirstName = "Peter",
            ChildSurname = "Doe",
            ChildDateOfBirth = "2016-09-08",
            ChildSchoolUrn = "654321",
            EligibilityEndDate = "2025-07-31"
        };

        var applicationData3 = new ApplicationBulkImportData
        {
            ParentFirstName = "Bob",
            ParentSurname = "Wilson",
            ParentDateOfBirth = "1988-05-10",
            ParentNino = "EF345678G",
            ParentEmail = "bob.wilson@example.com",
            ChildFirstName = "Alice",
            ChildSurname = "Wilson",
            ChildDateOfBirth = "2014-12-20",
            ChildSchoolUrn = "789012",
            EligibilityEndDate = "2025-07-31"
        };

        var request = new ApplicationBulkImportJsonRequest { Applications = new List<ApplicationBulkImportData> { applicationData1, applicationData2, applicationData3 } };
        var allowedLocalAuthorityIds = new List<int> { 1, 2 }; // LA 1 and 2 are allowed, but not 3

        var establishment1 = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1, // Allowed
            EstablishmentName = "Test School 1"
        };

        var establishment2 = new DomainEstablishment
        {
            EstablishmentId = 654321,
            LocalAuthorityId = 2, // Allowed
            EstablishmentName = "Test School 2"
        };

        var establishment3 = new DomainEstablishment
        {
            EstablishmentId = 789012,
            LocalAuthorityId = 3, // Not allowed
            EstablishmentName = "Test School 3"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment1 },
            { "654321", establishment2 },
            { "789012", establishment3 }
        };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(2); // First two should be allowed
        result.FailedImports.Should().Be(1); // Third should be rejected
        result.TotalRecords.Should().Be(3);
        result.Errors.Should().Contain("Row 3: You do not have permission to create applications for this establishment's local authority");
    }


    [Test]
    public async Task ExecuteFromJson_Should_Handle_Mixed_Authorization_And_Other_Errors()
    {
        // Arrange
        var applicationData1 = new ApplicationBulkImportData
        {
            ParentFirstName = "John",
            ParentSurname = "Smith",
            ParentDateOfBirth = "1985-03-15",
            ParentNino = "AB123456C",
            ParentEmail = "john.smith@example.com",
            ChildFirstName = "Emma",
            ChildSurname = "Smith",
            ChildDateOfBirth = "2015-04-12",
            ChildSchoolUrn = "123456",
            EligibilityEndDate = "2025-07-31"
        };

        var applicationData2 = new ApplicationBulkImportData
        {
            ParentFirstName = "Jane",
            ParentSurname = "Doe",
            ParentDateOfBirth = "1990-02-20",
            ParentNino = "CD789012E",
            ParentEmail = "jane.doe@example.com",
            ChildFirstName = "Peter",
            ChildSurname = "Doe",
            ChildDateOfBirth = "2016-09-08",
            ChildSchoolUrn = "999999", // Non-existent establishment
            EligibilityEndDate = "2025-07-31"
        };

        var applicationData3 = new ApplicationBulkImportData
        {
            ParentFirstName = "Bob",
            ParentSurname = "Wilson",
            ParentDateOfBirth = "1988-05-10",
            ParentNino = "EF345678G",
            ParentEmail = "bob.wilson@example.com",
            ChildFirstName = "Alice",
            ChildSurname = "Wilson",
            ChildDateOfBirth = "2014-12-20",
            ChildSchoolUrn = "654321",
            EligibilityEndDate = "2025-07-31"
        };

        var request = new ApplicationBulkImportJsonRequest { Applications = new List<ApplicationBulkImportData> { applicationData1, applicationData2, applicationData3 } };
        var allowedLocalAuthorityIds = new List<int> { 1 }; // Only LA 1 is allowed

        var establishment1 = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1, // Allowed
            EstablishmentName = "Test School 1"
        };

        var establishment3 = new DomainEstablishment
        {
            EstablishmentId = 654321,
            LocalAuthorityId = 2, // Not allowed
            EstablishmentName = "Test School 3"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment1 },
            { "654321", establishment3 }
            // Note: establishment 999999 is missing - this will cause an "establishment not found" error
        };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(1); // Only the first one should succeed
        result.FailedImports.Should().Be(2); // Both others should fail
        result.TotalRecords.Should().Be(3);
        result.Errors.Should().Contain("Row 2: Establishment with URN 999999 not found");
        result.Errors.Should().Contain("Row 3: You do not have permission to create applications for this establishment's local authority");
    }
    [Test]
    public async Task ExecuteFromJson_Should_Handle_Invalid_Data()
    {
        // Arrange
        var applicationData1 = new ApplicationBulkImportData
        {
            ParentFirstName = "", // Invalid - empty
            ParentSurname = "Smith",
            ParentDateOfBirth = "1985-03-15",
            ParentNino = "AB123456C",
            ParentEmail = "john.smith@example.com",
            ChildFirstName = "Emma",
            ChildSurname = "Smith",
            ChildDateOfBirth = "2015-04-12",
            ChildSchoolUrn = "123456",
            EligibilityEndDate = "2025-07-31"
        };

        var applicationData2 = new ApplicationBulkImportData
        {
            ParentFirstName = "Jane",
            ParentSurname = "", // Invalid - empty
            ParentDateOfBirth = "1990-02-20",
            ParentNino = "CD789012E",
            ParentEmail = "jane.doe@example.com",
            ChildFirstName = "Peter",
            ChildSurname = "Doe",
            ChildDateOfBirth = "2016-09-08",
            ChildSchoolUrn = "654321",
            EligibilityEndDate = "2025-07-31"
        };

        var applicationData3 = new ApplicationBulkImportData
        {
            ParentFirstName = "Bob",
            ParentSurname = "Wilson",
            ParentDateOfBirth = "invalid-date", // Invalid date format
            ParentNino = "EF345678G",
            ParentEmail = "bob.wilson@example.com",
            ChildFirstName = "Alice",
            ChildSurname = "Wilson",
            ChildDateOfBirth = "2014-12-20",
            ChildSchoolUrn = "789012",
            EligibilityEndDate = "2025-07-31"
        };

        var request = new ApplicationBulkImportJsonRequest { Applications = new List<ApplicationBulkImportData> { applicationData1, applicationData2, applicationData3 } };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        // Setup mock for URN lookup
        var establishmentLookup = new Dictionary<string, DomainEstablishment>();
        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);

        // Setup audit mock - won't actually be called, but need to set it up for teardown
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Override verification for this test
        _mockAuditGateway.Verify(x => x.CreateAuditEntry(It.IsAny<AuditType>(), It.IsAny<string>()), Times.Never);

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(3);
        result.TotalRecords.Should().Be(3);
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().Contain(error => error.Contains("Row 1:") && error.Contains("Parent first name"));
        result.Errors.Should().Contain(error => error.Contains("Row 2:") && error.Contains("Parent surname"));
        result.Errors.Should().Contain(error => error.Contains("Row 3:") && error.Contains("Parent date of birth"));
    }
    [Test]
    public async Task ExecuteFromJson_Should_Handle_BulkImport_Gateway_Exception()
    {
        // Arrange
        var applicationData = new ApplicationBulkImportData
        {
            ParentFirstName = "John",
            ParentSurname = "Smith",
            ParentDateOfBirth = "1985-03-15",
            ParentNino = "AB123456C",
            ParentEmail = "john.smith@example.com",
            ChildFirstName = "Emma",
            ChildSurname = "Smith",
            ChildDateOfBirth = "2015-04-12",
            ChildSchoolUrn = "123456",
            EligibilityEndDate = "2025-07-31"
        };

        var request = new ApplicationBulkImportJsonRequest { Applications = new List<ApplicationBulkImportData> { applicationData } };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        var establishment = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1,
            EstablishmentName = "Test School"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment }
        };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .ThrowsAsync(new Exception("Database error"));
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Don't verify audit as it won't be called on exception
        _mockAuditGateway.Verify(x => x.CreateAuditEntry(AuditType.Administration, string.Empty), Times.Never);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Import failed - error during bulk database operation.");
        result.Errors.Should().Contain("Error during bulk import: Database error");
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(1);
    }
    [Test]
    public void ExecuteFromJson_Should_Handle_GetEstablishments_Gateway_Exception() // Removed async
    {
        // Arrange
        var applicationData = new ApplicationBulkImportData
        {
            ParentFirstName = "John",
            ParentSurname = "Smith",
            ParentDateOfBirth = "1985-03-15",
            ParentNino = "AB123456C",
            ParentEmail = "john.smith@example.com",
            ChildFirstName = "Emma",
            ChildSurname = "Smith",
            ChildDateOfBirth = "2015-04-12",
            ChildSchoolUrn = "123456",
            EligibilityEndDate = "2025-07-31"
        };

        var request = new ApplicationBulkImportJsonRequest { Applications = new List<ApplicationBulkImportData> { applicationData } };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ThrowsAsync(new Exception("Database connection error"));

        // Act & Assert - The exception should bubble up
        var exception = Assert.ThrowsAsync<Exception>(async () => await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds));

        exception.Should().NotBeNull();
        exception!.Message.Should().Be("Database connection error");
    }


    [Test]
    public async Task ExecuteFromJson_Should_Handle_Partial_Success_Scenario()
    {
        // Arrange
        var applicationData1 = new ApplicationBulkImportData
        {
            ParentFirstName = "John",
            ParentSurname = "Smith",
            ParentDateOfBirth = "1985-03-15",
            ParentNino = "AB123456C",
            ParentEmail = "john.smith@example.com",
            ChildFirstName = "Emma",
            ChildSurname = "Smith",
            ChildDateOfBirth = "2015-04-12",
            ChildSchoolUrn = "123456",
            EligibilityEndDate = "2025-07-31"
        };

        var applicationData2 = new ApplicationBulkImportData
        {
            ParentFirstName = "", // Invalid - empty
            ParentSurname = "Doe",
            ParentDateOfBirth = "1990-02-20",
            ParentNino = "CD789012E",
            ParentEmail = "jane.doe@example.com",
            ChildFirstName = "Peter",
            ChildSurname = "Doe",
            ChildDateOfBirth = "654321", // Invalid DOB format
            ChildSchoolUrn = "654321",
            EligibilityEndDate = "2025-07-31"
        };

        var applicationData3 = new ApplicationBulkImportData
        {
            ParentFirstName = "Bob",
            ParentSurname = "Wilson",
            ParentDateOfBirth = "1988-05-10",
            ParentNino = "EF345678G",
            ParentEmail = "bob.wilson@example.com",
            ChildFirstName = "Alice",
            ChildSurname = "Wilson",
            ChildDateOfBirth = "2014-12-20",
            ChildSchoolUrn = "789012",
            EligibilityEndDate = "2025-07-31"
        };

        var request = new ApplicationBulkImportJsonRequest { Applications = new List<ApplicationBulkImportData> { applicationData1, applicationData2, applicationData3 } };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        var establishment1 = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1,
            EstablishmentName = "Test School 1"
        };

        var establishment3 = new DomainEstablishment
        {
            EstablishmentId = 789012,
            LocalAuthorityId = 3,
            EstablishmentName = "Test School 3"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment1 },
            { "789012", establishment3 }
        };

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(2);
        result.FailedImports.Should().Be(1);
        result.TotalRecords.Should().Be(3);
        result.Message.Should().Contain("Import partially completed");
        result.Errors.Should().Contain(error => error.Contains("Row 2:") && error.Contains("Parent first name"));
    }
    [Test]
    public async Task ExecuteFromJson_Should_Create_Applications_With_Correct_Properties()
    {
        // Arrange
        var applicationData = new ApplicationBulkImportData
        {
            ParentFirstName = "John",
            ParentSurname = "Smith",
            ParentDateOfBirth = "1985-03-15",
            ParentNino = "AB123456C",
            ParentEmail = "john.smith@example.com",
            ChildFirstName = "Emma",
            ChildSurname = "Smith",
            ChildDateOfBirth = "2015-04-12",
            ChildSchoolUrn = "123456",
            EligibilityEndDate = "2025-07-31"
        };

        var request = new ApplicationBulkImportJsonRequest { Applications = new List<ApplicationBulkImportData> { applicationData } };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        var establishment = new DomainEstablishment
        {
            EstablishmentId = 123456,
            LocalAuthorityId = 1,
            EstablishmentName = "Test School"
        };

        var establishmentLookup = new Dictionary<string, DomainEstablishment>
        {
            { "123456", establishment }
        };

        Application capturedApplication = null!;

        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);
        _mockApplicationGateway.Setup(x => x.BulkImportApplications(It.IsAny<IEnumerable<Application>>()))
            .Callback<IEnumerable<Application>>(apps => capturedApplication = apps.First())
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(1);

        capturedApplication.Should().NotBeNull();
        capturedApplication.ParentFirstName.Should().Be("John");
        capturedApplication.ParentLastName.Should().Be("Smith");
        capturedApplication.ParentDateOfBirth.Should().Be(new DateTime(1985, 3, 15));
        capturedApplication.ParentNationalInsuranceNumber.Should().Be("AB123456C");
        capturedApplication.ParentEmail.Should().Be("john.smith@example.com");
        capturedApplication.ChildFirstName.Should().Be("Emma");
        capturedApplication.ChildLastName.Should().Be("Smith");
        capturedApplication.ChildDateOfBirth.Should().Be(new DateTime(2015, 4, 12));
        capturedApplication.EstablishmentId.Should().Be(123456);
        capturedApplication.LocalAuthorityId.Should().Be(1);
        capturedApplication.Type.Should().Be(CheckEligibilityType.FreeSchoolMeals);
        capturedApplication.Status.Should().Be(Domain.Enums.ApplicationStatus.SentForReview);
        capturedApplication.ApplicationID.Should().NotBeNullOrEmpty();
        capturedApplication.Reference.Should().NotBeNullOrEmpty();
        capturedApplication.Created.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        capturedApplication.Updated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    #endregion ExecuteFromJson Tests


    [Test]
    public async Task Execute_Should_Handle_Invalid_Child_Date_Of_Birth_CSV()
    {
        // Arrange
        var csvContent = "Parent First Name,Parent Surname,Parent DOB,Parent Nino,Parent Email Address,Child First Name,Child Surname,Child Date of Birth,Child School URN,Eligibility End Date\n" +
                        "John,Smith,1985-03-15,AB123456C,john.smith@example.com,Emma,Smith,invalid-date,123456,2025-07-31\n" +
                        "Jane,Doe,1990-02-20,CD789012E,jane.doe@example.com,Peter,Doe,,654321,2025-07-31";

        var fileMock = CreateMockFile(csvContent, "text/csv");
        var request = new ApplicationBulkImportRequest { File = fileMock.Object };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        // Setup mock for URN lookup
        var establishmentLookup = new Dictionary<string, DomainEstablishment>();
        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);

        // Setup audit mock - won't actually be called, but need to set it up for teardown
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Override verification for this test
        _mockAuditGateway.Verify(x => x.CreateAuditEntry(It.IsAny<AuditType>(), It.IsAny<string>()), Times.Never);

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(2);
        result.TotalRecords.Should().Be(2);
        result.Errors.Should().Contain(error => error.Contains("Row 1:") && error.Contains("Child date of birth"));
        result.Errors.Should().Contain(error => error.Contains("Row 2:") && error.Contains("Child Date of Birth is required"));
    }


    [Test]
    public async Task ExecuteFromJson_Should_Handle_Invalid_Child_Date_Of_Birth()
    {
        // Arrange
        var applicationData1 = new ApplicationBulkImportData
        {
            ParentFirstName = "John",
            ParentSurname = "Smith",
            ParentDateOfBirth = "1985-03-15",
            ParentNino = "AB123456C",
            ParentEmail = "john.smith@example.com",
            ChildFirstName = "Emma",
            ChildSurname = "Smith",
            ChildDateOfBirth = "invalid-date", // Invalid date format
            ChildSchoolUrn = "123456",
            EligibilityEndDate = "2025-07-31"
        };

        var applicationData2 = new ApplicationBulkImportData
        {
            ParentFirstName = "Jane",
            ParentSurname = "Doe",
            ParentDateOfBirth = "1990-02-20",
            ParentNino = "CD789012E",
            ParentEmail = "jane.doe@example.com",
            ChildFirstName = "Peter",
            ChildSurname = "Doe",
            ChildDateOfBirth = "", // Empty date
            ChildSchoolUrn = "654321",
            EligibilityEndDate = "2025-07-31"
        };

        var request = new ApplicationBulkImportJsonRequest { Applications = new List<ApplicationBulkImportData> { applicationData1, applicationData2 } };
        var allowedLocalAuthorityIds = new List<int> { 1, 2, 3 };

        // Setup mock for URN lookup
        var establishmentLookup = new Dictionary<string, DomainEstablishment>();
        _mockApplicationGateway.Setup(x => x.GetEstablishmentEntitiesByUrns(It.IsAny<List<string>>()))
            .ReturnsAsync(establishmentLookup);

        // Setup audit mock - won't actually be called, but need to set it up for teardown
        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Override verification for this test
        _mockAuditGateway.Verify(x => x.CreateAuditEntry(It.IsAny<AuditType>(), It.IsAny<string>()), Times.Never);

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulImports.Should().Be(0);
        result.FailedImports.Should().Be(2);
        result.TotalRecords.Should().Be(2);
        result.Errors.Should().Contain(error => error.Contains("Row 1:") && error.Contains("Child date of birth"));
        result.Errors.Should().Contain(error => error.Contains("Row 2:") && error.Contains("Child Date of Birth is required"));
    }
}
