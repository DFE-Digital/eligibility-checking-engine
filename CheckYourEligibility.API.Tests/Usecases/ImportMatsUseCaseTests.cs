using AutoFixture;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.CsvImport;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Tests.Properties;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class ImportMatsUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IAdministration>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<ImportMatsUseCase>>(MockBehavior.Loose);
        _sut = new ImportMatsUseCase(_mockGateway.Object, _mockAuditGateway.Object, _mockLogger.Object);
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<IAdministration> _mockGateway;
    private Mock<IAudit> _mockAuditGateway;
    private Mock<ILogger<ImportMatsUseCase>> _mockLogger;
    private ImportMatsUseCase _sut;
    private Fixture _fixture;

    [Test]
    public async Task Execute_Should_ImportMats_When_File_Is_Valid()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        var content = Resources.small_mat;
        var fileName = "test.csv";
        var ms = new MemoryStream();
        var writer = new StreamWriter(ms);
        writer.Write(content);
        writer.Flush();
        ms.Position = 0;
        fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(ms.Length);
        fileMock.Setup(f => f.ContentType).Returns("text/csv");

        _mockGateway.Setup(s => s.ImportMats(It.IsAny<List<MatRow>>())).Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Administration, string.Empty, null))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(fileMock.Object);

        // Assert
        _mockGateway.Verify(s => s.ImportMats(It.IsAny<List<MatRow>>()), Times.Once);
    }

    [Test]
    public void Execute_Should_Throw_InvalidDataException_When_File_Is_Null()
    {
        // Act
        var act = async () => await _sut.Execute(null);

        // Assert
        act.Should().ThrowAsync<InvalidDataException>().WithMessage("CSV file required.");
    }

    [Test]
    public void Execute_Should_Throw_InvalidDataException_When_File_Is_Not_CSV()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("application/json");

        // Act
        var act = async () => await _sut.Execute(fileMock.Object);

        // Assert
        act.Should().ThrowAsync<InvalidDataException>().WithMessage("CSV file required.");
    }

    [Test]
    public void Execute_Should_Throw_InvalidDataException_When_File_Content_Is_Invalid()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        var content = "InvalidContent";
        var fileName = "test.csv";
        var ms = new MemoryStream();
        var writer = new StreamWriter(ms);
        writer.Write(content);
        writer.Flush();
        ms.Position = 0;
        fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(ms.Length);
        fileMock.Setup(f => f.ContentType).Returns("text/csv");

        // Act
        var act = async () => await _sut.Execute(fileMock.Object);

        // Assert
        act.Should().ThrowAsync<InvalidDataException>().WithMessage("Invalid file content.");
    }

    [Test]
    public void Execute_Should_LogError_And_Throw_InvalidDataException_On_Exception()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        var content = Resources.small_mat;
        var fileName = "test.csv";
        var ms = new MemoryStream();
        var writer = new StreamWriter(ms);
        writer.Write(content);
        writer.Flush();
        ms.Position = 0;
        fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(ms.Length);
        fileMock.Setup(f => f.ContentType).Returns("text/csv");

        _mockGateway.Setup(s => s.ImportMats(It.IsAny<List<MatRow>>()))
            .Throws(new Exception("Test exception"));

        // Act
        var act = async () => await _sut.Execute(fileMock.Object);

        // Assert
        act.Should().ThrowAsync<InvalidDataException>().WithMessage($"{fileName} - {{}} :- Test exception, ");
    }
}