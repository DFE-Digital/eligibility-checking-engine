using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;

namespace CheckYourEligibility.API.Tests.UsecCases;

[TestFixture]
public class BulkDeleteApplicationsUseCaseTests : TestBase.TestBase
{
    private new Fixture _fixture = null!;
    private Mock<IApplication> _mockApplicationGateway = null!;
    private Mock<IAudit> _mockAuditGateway = null!;
    private Mock<ILogger<BulkDeleteApplicationsUseCase>> _mockLogger = null!;
    private BulkDeleteApplicationsUseCase _sut = null!;

    [SetUp]
    public void Setup()
    {
        _fixture = new Fixture();
        _mockApplicationGateway = new Mock<IApplication>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<BulkDeleteApplicationsUseCase>>();

        _sut = new BulkDeleteApplicationsUseCase(
            _mockApplicationGateway.Object,
            _mockAuditGateway.Object,
            _mockLogger.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _mockApplicationGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    #region Execute Tests (File Upload)

    [Test]
    public async Task Execute_NullFile_ReturnsErrorResponse()
    {
        // Arrange
        var request = new ApplicationBulkDeleteRequest { File = null! };
        var allowedLocalAuthorityIds = new List<int> { 1 };

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Delete failed - file is required.");
        result.Errors.Should().Contain("File required.");
        result.SuccessfulDeletions.Should().Be(0);
        result.FailedDeletions.Should().Be(0);
        result.TotalRecords.Should().Be(0);
    }

    [Test]
    public async Task Execute_InvalidFileType_ReturnsErrorResponse()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.ContentType).Returns("text/plain");

        var request = new ApplicationBulkDeleteRequest { File = mockFile.Object };
        var allowedLocalAuthorityIds = new List<int> { 1 };

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Delete failed - CSV or JSON file is required.");
        result.Errors.Should().Contain("CSV or JSON file required.");
        result.SuccessfulDeletions.Should().Be(0);
        result.FailedDeletions.Should().Be(0);
        result.TotalRecords.Should().Be(0);
    }

    [Test]
    public async Task Execute_ValidCsvFile_SuccessfulDeletion()
    {
        // Arrange
        var guid1 = Guid.NewGuid().ToString();
        var guid2 = Guid.NewGuid().ToString();
        var csvContent = $"ApplicationGuid\n{guid1}\n{guid2}";
        var csvBytes = Encoding.UTF8.GetBytes(csvContent);
        var stream = new MemoryStream(csvBytes);

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.ContentType).Returns("text/csv");
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

        var request = new ApplicationBulkDeleteRequest { File = mockFile.Object };
        var allowedLocalAuthorityIds = new List<int> { 1 };

        var localAuthorityMap = new Dictionary<string, int>
        {
            { guid1, 1 },
            { guid2, 1 }
        };

        var deletionResults = new Dictionary<string, bool>
        {
            { guid1, true },
            { guid2, true }
        };

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdsForApplications(It.Is<IEnumerable<string>>(guids => 
            guids.Count() == 2 && guids.Contains(guid1) && guids.Contains(guid2))))
            .ReturnsAsync(localAuthorityMap);

        _mockApplicationGateway.Setup(x => x.BulkDeleteApplications(It.Is<IEnumerable<string>>(guids => 
            guids.Count() == 2 && guids.Contains(guid1) && guids.Contains(guid2))))
            .ReturnsAsync(deletionResults);

        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulDeletions.Should().Be(2);
        result.FailedDeletions.Should().Be(0);
        result.TotalRecords.Should().Be(2);
        result.Message.Should().Be("Delete completed successfully - all 2 records deleted.");
        result.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task Execute_ValidJsonFile_SuccessfulDeletion()
    {
        // Arrange
        var guid1 = Guid.NewGuid().ToString();
        var guid2 = Guid.NewGuid().ToString();
        var jsonContent = $"[\"{guid1}\", \"{guid2}\"]";
        var jsonBytes = Encoding.UTF8.GetBytes(jsonContent);
        var stream = new MemoryStream(jsonBytes);

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.ContentType).Returns("application/json");
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

        var request = new ApplicationBulkDeleteRequest { File = mockFile.Object };
        var allowedLocalAuthorityIds = new List<int> { 1 };

        var localAuthorityMap = new Dictionary<string, int>
        {
            { guid1, 1 },
            { guid2, 1 }
        };

        var deletionResults = new Dictionary<string, bool>
        {
            { guid1, true },
            { guid2, true }
        };

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdsForApplications(It.Is<IEnumerable<string>>(guids => 
            guids.Count() == 2 && guids.Contains(guid1) && guids.Contains(guid2))))
            .ReturnsAsync(localAuthorityMap);

        _mockApplicationGateway.Setup(x => x.BulkDeleteApplications(It.Is<IEnumerable<string>>(guids => 
            guids.Count() == 2 && guids.Contains(guid1) && guids.Contains(guid2))))
            .ReturnsAsync(deletionResults);

        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulDeletions.Should().Be(2);
        result.FailedDeletions.Should().Be(0);
        result.TotalRecords.Should().Be(2);
        result.Message.Should().Be("Delete completed successfully - all 2 records deleted.");
        result.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task Execute_UnauthorizedLocalAuthority_ReturnsPartialFailure()
    {
        // Arrange
        var guid1 = Guid.NewGuid().ToString();
        var guid2 = Guid.NewGuid().ToString();
        var csvContent = $"ApplicationGuid\n{guid1}\n{guid2}";
        var csvBytes = Encoding.UTF8.GetBytes(csvContent);
        var stream = new MemoryStream(csvBytes);

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.ContentType).Returns("text/csv");
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

        var request = new ApplicationBulkDeleteRequest { File = mockFile.Object };
        var allowedLocalAuthorityIds = new List<int> { 1 }; // Only LA 1 allowed

        var localAuthorityMap = new Dictionary<string, int>
        {
            { guid1, 1 }, // Allowed
            { guid2, 2 }  // Not allowed
        };

        var deletionResults = new Dictionary<string, bool>
        {
            { guid1, true }
        };

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdsForApplications(It.Is<IEnumerable<string>>(guids => 
            guids.Count() == 2 && guids.Contains(guid1) && guids.Contains(guid2))))
            .ReturnsAsync(localAuthorityMap);

        _mockApplicationGateway.Setup(x => x.BulkDeleteApplications(It.Is<IEnumerable<string>>(guids => 
            guids.Count() == 1 && guids.Contains(guid1))))
            .ReturnsAsync(deletionResults);

        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulDeletions.Should().Be(1);
        result.FailedDeletions.Should().Be(1);
        result.TotalRecords.Should().Be(2);
        result.Message.Should().Be("Delete partially completed - 1 records deleted, 1 failed. Please check the errors above.");
        result.Errors.Should().Contain($"GUID {guid2}: You do not have permission to delete applications for this establishment's local authority");
    }

    [Test]
    public async Task Execute_InvalidGuidsInFile_ReturnsValidationErrors()
    {
        // Arrange
        var validGuid = Guid.NewGuid().ToString();
        var invalidGuid = "invalid-guid";
        var csvContent = $"ApplicationGuid\n{validGuid}\n{invalidGuid}\n";
        var csvBytes = Encoding.UTF8.GetBytes(csvContent);
        var stream = new MemoryStream(csvBytes);

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.ContentType).Returns("text/csv");
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

        var request = new ApplicationBulkDeleteRequest { File = mockFile.Object };
        var allowedLocalAuthorityIds = new List<int> { 1 };

        var localAuthorityMap = new Dictionary<string, int>
        {
            { validGuid, 1 }
        };

        var deletionResults = new Dictionary<string, bool>
        {
            { validGuid, true }
        };

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdsForApplications(It.Is<IEnumerable<string>>(guids => 
            guids.Count() == 1 && guids.Contains(validGuid))))
            .ReturnsAsync(localAuthorityMap);

        _mockApplicationGateway.Setup(x => x.BulkDeleteApplications(It.Is<IEnumerable<string>>(guids => 
            guids.Count() == 1 && guids.Contains(validGuid))))
            .ReturnsAsync(deletionResults);

        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulDeletions.Should().Be(1);
        result.FailedDeletions.Should().Be(1);
        result.TotalRecords.Should().Be(2); // valid guid + invalid guid (empty rows are ignored)
        result.Errors.Should().Contain($"Row 2: Invalid GUID format: {invalidGuid}");
        result.Message.Should().Be("Delete partially completed - 1 records deleted, 1 failed. Please check the errors above.");
    }

    [Test]
    public async Task Execute_DuplicateGuids_ReturnsValidationErrors()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();
        var csvContent = $"ApplicationGuid\n{guid}\n{guid}";
        var csvBytes = Encoding.UTF8.GetBytes(csvContent);
        var stream = new MemoryStream(csvBytes);

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.ContentType).Returns("text/csv");
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

        var request = new ApplicationBulkDeleteRequest { File = mockFile.Object };
        var allowedLocalAuthorityIds = new List<int> { 1 };

        var localAuthorityMap = new Dictionary<string, int>
        {
            { guid, 1 }
        };

        var deletionResults = new Dictionary<string, bool>
        {
            { guid, true }
        };

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdsForApplications(It.Is<IEnumerable<string>>(guids => 
            guids.Count() == 1 && guids.Contains(guid))))
            .ReturnsAsync(localAuthorityMap);

        _mockApplicationGateway.Setup(x => x.BulkDeleteApplications(It.Is<IEnumerable<string>>(guids => 
            guids.Count() == 1 && guids.Contains(guid))))
            .ReturnsAsync(deletionResults);

        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulDeletions.Should().Be(1);
        result.FailedDeletions.Should().Be(1);
        result.TotalRecords.Should().Be(2);
        result.Errors.Should().Contain($"Row 2: Duplicate GUID: {guid}");
        result.Message.Should().Be("Delete partially completed - 1 records deleted, 1 failed. Please check the errors above.");
    }

    [Test]
    public async Task Execute_EmptyFile_ReturnsErrorResponse()
    {
        // Arrange
        var csvContent = "ApplicationGuid\n";
        var csvBytes = Encoding.UTF8.GetBytes(csvContent);
        var stream = new MemoryStream(csvBytes);

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.ContentType).Returns("text/csv");
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

        var request = new ApplicationBulkDeleteRequest { File = mockFile.Object };
        var allowedLocalAuthorityIds = new List<int> { 1 };

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Delete failed - no valid GUIDs found in the file.");
        result.Errors.Should().Contain("Invalid file content - no GUIDs found.");
        result.SuccessfulDeletions.Should().Be(0);
        result.FailedDeletions.Should().Be(0);
    }

    [Test]
    public async Task Execute_DatabaseError_ReturnsErrorResponse()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();
        var csvContent = $"ApplicationGuid\n{guid}";
        var csvBytes = Encoding.UTF8.GetBytes(csvContent);
        var stream = new MemoryStream(csvBytes);

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.ContentType).Returns("text/csv");
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

        var request = new ApplicationBulkDeleteRequest { File = mockFile.Object };
        var allowedLocalAuthorityIds = new List<int> { 1 };

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdsForApplications(It.IsAny<IEnumerable<string>>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _sut.Execute(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Delete failed - error during bulk database operation.");
        result.Errors.Should().Contain("Error during bulk delete: Database error");
        result.SuccessfulDeletions.Should().Be(0);
        result.FailedDeletions.Should().Be(1);
        result.TotalRecords.Should().Be(1);
    }

    #endregion

    #region ExecuteFromJson Tests

    [Test]
    public async Task ExecuteFromJson_ValidRequest_SuccessfulDeletion()
    {
        // Arrange
        var guid1 = Guid.NewGuid().ToString();
        var guid2 = Guid.NewGuid().ToString();
        var request = new ApplicationBulkDeleteJsonRequest
        {
            ApplicationGuids = new List<string> { guid1, guid2 }
        };
        var allowedLocalAuthorityIds = new List<int> { 1 };

        var localAuthorityMap = new Dictionary<string, int>
        {
            { guid1, 1 },
            { guid2, 1 }
        };

        var deletionResults = new Dictionary<string, bool>
        {
            { guid1, true },
            { guid2, true }
        };

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdsForApplications(It.Is<IEnumerable<string>>(guids => 
            guids.Count() == 2 && guids.Contains(guid1) && guids.Contains(guid2))))
            .ReturnsAsync(localAuthorityMap);

        _mockApplicationGateway.Setup(x => x.BulkDeleteApplications(It.Is<IEnumerable<string>>(guids => 
            guids.Count() == 2 && guids.Contains(guid1) && guids.Contains(guid2))))
            .ReturnsAsync(deletionResults);

        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulDeletions.Should().Be(2);
        result.FailedDeletions.Should().Be(0);
        result.TotalRecords.Should().Be(2);
        result.Message.Should().Be("Delete completed successfully - all 2 records deleted.");
        result.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ExecuteFromJson_EmptyRequest_ReturnsErrorResponse()
    {
        // Arrange
        var request = new ApplicationBulkDeleteJsonRequest
        {
            ApplicationGuids = new List<string>()
        };
        var allowedLocalAuthorityIds = new List<int> { 1 };

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Delete failed - no application GUIDs provided.");
        result.Errors.Should().Contain("Application GUIDs required.");
        result.SuccessfulDeletions.Should().Be(0);
        result.FailedDeletions.Should().Be(0);
        result.TotalRecords.Should().Be(0);
    }

    [Test]
    public async Task ExecuteFromJson_NullRequest_ReturnsErrorResponse()
    {
        // Arrange
        var request = new ApplicationBulkDeleteJsonRequest
        {
            ApplicationGuids = null!
        };
        var allowedLocalAuthorityIds = new List<int> { 1 };

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Delete failed - no application GUIDs provided.");
        result.Errors.Should().Contain("Application GUIDs required.");
        result.SuccessfulDeletions.Should().Be(0);
        result.FailedDeletions.Should().Be(0);
        result.TotalRecords.Should().Be(0);
    }

    [Test]
    public async Task ExecuteFromJson_AdminUser_CanDeleteFromAnyLocalAuthority()
    {
        // Arrange
        var guid1 = Guid.NewGuid().ToString();
        var guid2 = Guid.NewGuid().ToString();
        var request = new ApplicationBulkDeleteJsonRequest
        {
            ApplicationGuids = new List<string> { guid1, guid2 }
        };
        var allowedLocalAuthorityIds = new List<int> { 0 }; // Admin user (ID 0)

        var localAuthorityMap = new Dictionary<string, int>
        {
            { guid1, 1 },
            { guid2, 2 } // Different local authorities
        };

        var deletionResults = new Dictionary<string, bool>
        {
            { guid1, true },
            { guid2, true }
        };

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdsForApplications(It.Is<IEnumerable<string>>(guids => 
            guids.Count() == 2 && guids.Contains(guid1) && guids.Contains(guid2))))
            .ReturnsAsync(localAuthorityMap);

        _mockApplicationGateway.Setup(x => x.BulkDeleteApplications(It.Is<IEnumerable<string>>(guids => 
            guids.Count() == 2 && guids.Contains(guid1) && guids.Contains(guid2))))
            .ReturnsAsync(deletionResults);

        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulDeletions.Should().Be(2);
        result.FailedDeletions.Should().Be(0);
        result.TotalRecords.Should().Be(2);
        result.Message.Should().Be("Delete completed successfully - all 2 records deleted.");
        result.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ExecuteFromJson_ApplicationNotFound_ReturnsPartialFailure()
    {
        // Arrange
        var guid1 = Guid.NewGuid().ToString();
        var guid2 = Guid.NewGuid().ToString();
        var request = new ApplicationBulkDeleteJsonRequest
        {
            ApplicationGuids = new List<string> { guid1, guid2 }
        };
        var allowedLocalAuthorityIds = new List<int> { 1 };

        var localAuthorityMap = new Dictionary<string, int>
        {
            { guid1, 1 }
            // guid2 not found in map
        };

        var deletionResults = new Dictionary<string, bool>
        {
            { guid1, true },
            { guid2, false } // Not found/could not be deleted
        };

        _mockApplicationGateway.Setup(x => x.GetLocalAuthorityIdsForApplications(It.Is<IEnumerable<string>>(guids => 
            guids.Count() == 2 && guids.Contains(guid1) && guids.Contains(guid2))))
            .ReturnsAsync(localAuthorityMap);

        _mockApplicationGateway.Setup(x => x.BulkDeleteApplications(It.Is<IEnumerable<string>>(guids => 
            guids.Count() == 2 && guids.Contains(guid1) && guids.Contains(guid2))))
            .ReturnsAsync(deletionResults);

        _mockAuditGateway.Setup(x => x.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync("audit-id");

        // Act
        var result = await _sut.ExecuteFromJson(request, allowedLocalAuthorityIds);

        // Assert
        result.Should().NotBeNull();
        result.SuccessfulDeletions.Should().Be(1);
        result.FailedDeletions.Should().Be(1);
        result.TotalRecords.Should().Be(2);
        result.Message.Should().Be("Delete partially completed - 1 records deleted, 1 failed. Please check the errors above.");
        result.Errors.Should().Contain($"GUID {guid2}: Application not found or could not be deleted");
    }

    #endregion
}
