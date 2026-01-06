using AutoFixture;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.CsvImport;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class UpdateEstablishmentsPrivateBetaUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IAdministration>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<UpdateEstablishmentsPrivateBetaUseCase>>(MockBehavior.Loose);
        _sut = new UpdateEstablishmentsPrivateBetaUseCase(_mockGateway.Object, _mockAuditGateway.Object, _mockLogger.Object);
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
    private Mock<ILogger<UpdateEstablishmentsPrivateBetaUseCase>> _mockLogger;
    private UpdateEstablishmentsPrivateBetaUseCase _sut;
    private Fixture _fixture;

    private const string ValidCsvContent = "School URN,In Private Beta\n100718,Yes\n142364,No";

    [Test]
    public async Task Execute_Should_UpdateEstablishmentsPrivateBeta_When_File_Is_Valid()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        var content = ValidCsvContent;
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

        _mockGateway.Setup(s => s.UpdateEstablishmentsPrivateBeta(It.IsAny<List<EstablishmentPrivateBetaRow>>())).Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(fileMock.Object);

        // Assert
        _mockGateway.Verify(s => s.UpdateEstablishmentsPrivateBeta(It.IsAny<List<EstablishmentPrivateBetaRow>>()), Times.Once);
    }

    [Test]
    public async Task Execute_Should_Pass_Correct_Data_To_Gateway()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        var content = ValidCsvContent;
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

        List<EstablishmentPrivateBetaRow> capturedData = null;
        _mockGateway.Setup(s => s.UpdateEstablishmentsPrivateBeta(It.IsAny<List<EstablishmentPrivateBetaRow>>()))
            .Callback<IEnumerable<EstablishmentPrivateBetaRow>>(data => capturedData = data.ToList())
            .Returns(Task.CompletedTask);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.Administration, string.Empty))
            .ReturnsAsync(_fixture.Create<string>());

        // Act
        await _sut.Execute(fileMock.Object);

        // Assert
        capturedData.Should().NotBeNull();
        capturedData.Should().HaveCount(2);
        capturedData[0].EstablishmentId.Should().Be(100718);
        capturedData[0].InPrivateBeta.Should().BeTrue();
        capturedData[1].EstablishmentId.Should().Be(142364);
        capturedData[1].InPrivateBeta.Should().BeFalse();
    }

    [Test]
    public void Execute_Should_Throw_InvalidDataException_When_File_Is_Null()
    {
        // Act
        var act = async () => await _sut.Execute(null);

        // Assert
        act.Should().ThrowAsync<InvalidDataException>();
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
        act.Should().ThrowAsync<InvalidDataException>();
    }

    [Test]
    public void Execute_Should_Throw_InvalidDataException_When_File_Content_Is_Empty()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        var content = "School URN,In Private Beta";  // Only header, no data
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
}
