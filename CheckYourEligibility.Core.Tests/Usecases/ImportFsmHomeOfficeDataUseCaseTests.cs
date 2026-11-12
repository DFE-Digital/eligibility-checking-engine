using System.Text;
using CheckYourEligibility.Core.Domain;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.Core.Tests.UseCases;

[TestFixture]
public class ImportFsmHomeOfficeDataUseCaseTests : TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IAdministration>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<ImportFsmHomeOfficeDataUseCase>>(MockBehavior.Loose);
        _sut = new ImportFsmHomeOfficeDataUseCase(_mockGateway.Object, _mockAuditGateway.Object, _mockLogger.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<IAdministration> _mockGateway;
    private Mock<IAudit> _mockAuditGateway;
    private Mock<ILogger<ImportFsmHomeOfficeDataUseCase>> _mockLogger;
    private ImportFsmHomeOfficeDataUseCase _sut;

    [Test]
    public async Task Execute_Should_Throw_InvalidDataException_When_File_Is_Null()
    {
        // Arrange
        IFormFile file = null;

        // Act
        var act = async () => await _sut.Execute(file);

        // Assert
        await act.Should().ThrowExactlyAsync<InvalidDataException>().WithMessage("Csv data file is required.");
    }

    [Test]
    public async Task Execute_Should_Throw_InvalidDataException_When_File_Is_Not_CSV()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/plain");

        // Act
        var act = async () => await _sut.Execute(fileMock.Object);

        // Assert
        await act.Should().ThrowExactlyAsync<InvalidDataException>().WithMessage("Csv data file is required.");
    }

    [Test]
    public async Task Execute_Should_Process_CSV_File_And_Call_ImportHomeOfficeData()
    {
        // Arrange
        const string csv =
            "Type,AS Ref,Port Ref,Given Name,Family Name,DOB,Group Size,No of 4-17 year old in Group\r\n" +
            "Asylum,12/34/56789,MST/9011357,Alice,Smith,31/01/1990,4,2";
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/csv");
        fileMock.Setup(f => f.OpenReadStream())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes(csv)));

        List<FreeSchoolMealsHO> importedData = null!;
        _mockGateway.Setup(s => s.ImportHomeOfficeData(It.IsAny<List<FreeSchoolMealsHO>>()))
            .Callback<IEnumerable<FreeSchoolMealsHO>>(data => importedData = data.ToList())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Execute(fileMock.Object);

        // Assert
        importedData.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                FirstName = "Alice",
                LastName = "Smith",
                DateOfBirth = new DateTime(1990, 1, 31),
                NASS = "123456789",
                PortReferenceNumber = "MST/9011357"
            });
    }

    [Test]
    public async Task Execute_Should_Throw_InvalidDataException_When_CSV_File_Has_No_Content()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/csv");
        fileMock.Setup(f => f.FileName).Returns("test.csv");

        // Create empty CSV file
        var emptyCsv =
            "Type,AS Ref,Port Ref,Given Name,Family Name,DOB,Group Size,No of 4-17 year old in Group";
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(Encoding.UTF8.GetBytes(emptyCsv)));

        // Act
        var act = async () => await _sut.Execute(fileMock.Object);

        // Assert
        await act.Should().ThrowExactlyAsync<InvalidDataException>()
            .WithMessage("test.csv: the file contains no data rows.");
    }

    [TestCase("")]
    [TestCase("12345678")]
    [TestCase("12/34/5678")]
    public async Task Execute_Should_Reject_Invalid_AsylumSeekerReferenceNumber(string asylumSeekerReferenceNumber)
    {
        // Arrange
        var csv =
            "Type,AS Ref,Port Ref,Given Name,Family Name,DOB,Group Size,No of 4-17 year old in Group\r\n" +
            $"Asylum,{asylumSeekerReferenceNumber},TN4/8689288,Alice,Smith,31/01/1990,4,2";
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/csv");
        fileMock.Setup(f => f.FileName).Returns("test.csv");
        fileMock.Setup(f => f.OpenReadStream())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes(csv)));

        // Act
        var act = async () => await _sut.Execute(fileMock.Object);

        // Assert
        await act.Should().ThrowExactlyAsync<InvalidDataException>()
            .WithMessage("*Row 2: AS Ref must be nine digits, optionally formatted as 00/00/00000.*");
    }

    [TestCase("19900131")]
    [TestCase("1990-01-31")]
    [TestCase("1/1/1990")]
    [TestCase("31/13/1990")]
    [TestCase("11/31/1990")]
    [TestCase("")]
    public async Task Execute_Should_Reject_DateOfBirth_Not_In_DdMmYyyy_Format(string dateOfBirth)
    {
        // Arrange
        var csv =
            "Type,AS Ref,Port Ref,Given Name,Family Name,DOB,Group Size,No of 4-17 year old in Group\r\n" +
            $"Asylum,12/34/56789,TN4/8689288,Alice,Smith,{dateOfBirth},4,2";
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/csv");
        fileMock.Setup(f => f.FileName).Returns("test.csv");
        fileMock.Setup(f => f.OpenReadStream())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes(csv)));

        // Act
        var act = async () => await _sut.Execute(fileMock.Object);

        // Assert
        await act.Should().ThrowExactlyAsync<InvalidDataException>()
            .WithMessage("test.csv: 1 error(s) found, no data was imported. " +
                         "Row 2: DOB must be a valid date in the format dd/MM/yyyy, for example 31/01/1990.");
        _mockGateway.Verify(s => s.ImportHomeOfficeData(It.IsAny<IEnumerable<FreeSchoolMealsHO>>()), Times.Never);
    }

    [Test]
    public async Task Execute_Should_Report_All_Row_Errors_Without_Echoing_Data_Values()
    {
        // Arrange
        const string csv =
            "Type,AS Ref,Port Ref,Given Name,Family Name,DOB,Group Size,No of 4-17 year old in Group\r\n" +
            "A,25/09/04373,MST/9011355,SIDAD,PHUL,30/09/2003,2,1\r\n" +
            "A,24/12/05470,TN3/1446369,UMIN,BABA,11/31/1990,4,2\r\n" +
            "A,2412/05470,TN3/1446369,DORI,ALFRED,19/01/1985,4,2";
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/csv");
        fileMock.Setup(f => f.FileName).Returns("NASS.csv");
        fileMock.Setup(f => f.OpenReadStream())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes(csv)));

        // Act
        var act = async () => await _sut.Execute(fileMock.Object);

        // Assert
        var exception = (await act.Should().ThrowExactlyAsync<InvalidDataException>()).Which;
        exception.Message.Should().Be(
            "NASS.csv: 2 error(s) found, no data was imported. " +
            "Row 3: DOB must be a valid date in the format dd/MM/yyyy, for example 31/01/1990. " +
            "Row 4: AS Ref must be nine digits, optionally formatted as 00/00/00000.");
        exception.Message.Should().NotContainAny("UMIN", "BABA", "DORI", "11/31/1990", "2412/05470");
        _mockGateway.Verify(s => s.ImportHomeOfficeData(It.IsAny<IEnumerable<FreeSchoolMealsHO>>()), Times.Never);
    }

    [Test]
    public async Task Execute_Should_Limit_Reported_Row_Errors()
    {
        // Arrange
        var rows = Enumerable.Range(0, 25).Select(_ => "A,12/34/56789,MST/9011355,Alice,Smith,31-01-1990,4,2");
        var csv =
            "Type,AS Ref,Port Ref,Given Name,Family Name,DOB,Group Size,No of 4-17 year old in Group\r\n" +
            string.Join("\r\n", rows);
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/csv");
        fileMock.Setup(f => f.FileName).Returns("test.csv");
        fileMock.Setup(f => f.OpenReadStream())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes(csv)));

        // Act
        var act = async () => await _sut.Execute(fileMock.Object);

        // Assert
        await act.Should().ThrowExactlyAsync<InvalidDataException>()
            .WithMessage("test.csv: 25 error(s) found, no data was imported. *...and 5 more.");
    }

    [Test]
    public async Task Execute_Should_Accept_Rows_Without_Names()
    {
        // Arrange
        const string csv =
            "Type,AS Ref,Port Ref,Given Name,Family Name,DOB,Group Size,No of 4-17 year old in Group\r\n" +
            "A,26/05/00285,MST/9228215,,XAVIER,20/04/1997,3,2\r\n" +
            "A,25/10/05384,MST/9043705,,,02/06/1999,3,1";
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/csv");
        fileMock.Setup(f => f.OpenReadStream())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes(csv)));

        List<FreeSchoolMealsHO> importedData = null!;
        _mockGateway.Setup(s => s.ImportHomeOfficeData(It.IsAny<List<FreeSchoolMealsHO>>()))
            .Callback<IEnumerable<FreeSchoolMealsHO>>(data => importedData = data.ToList())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.Execute(fileMock.Object);

        // Assert
        result.Should().Be(new HomeOfficeImportResult(2, 1));
        importedData.Should().HaveCount(2);
        importedData.Select(x => x.LastName).Should().Equal("XAVIER", string.Empty);
        importedData.Should().OnlyContain(x => string.IsNullOrEmpty(x.FirstName));
    }

    [Test]
    public async Task Execute_Should_Reject_CSV_Without_A_Required_Header()
    {
        // Arrange
        const string csv =
            "Type,AS Ref,Port Ref,Given Name,DOB,Group Size,No of 4-17 year old in Group\r\n" +
            "Asylum,12/34/56789,MST/9011357,Alice,31/01/1990,4,2";
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/csv");
        fileMock.Setup(f => f.FileName).Returns("test.csv");
        fileMock.Setup(f => f.OpenReadStream())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes(csv)));

        // Act
        var act = async () => await _sut.Execute(fileMock.Object);

        // Assert
        await act.Should().ThrowExactlyAsync<InvalidDataException>()
            .WithMessage("test.csv: missing or misnamed column(s): Family Name. " +
                         "Expected columns: AS Ref, Port Ref, Given Name, Family Name, DOB.");
    }
}