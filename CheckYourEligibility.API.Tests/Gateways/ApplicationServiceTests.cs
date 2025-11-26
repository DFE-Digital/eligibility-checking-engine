// Ignore Spelling: Levenshtein

using System.Diagnostics.CodeAnalysis;
using AutoFixture;
using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Data.Mappings;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Gateways.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ApplicationStatus = CheckYourEligibility.API.Domain.Enums.ApplicationStatus;
using Establishment = CheckYourEligibility.API.Domain.Establishment;

namespace CheckYourEligibility.API.Tests;

[ExcludeFromCodeCoverage(Justification = "test")]
public class ApplicationServiceTests : TestBase.TestBase
{
    private IConfiguration _configuration;
    private IEligibilityCheckContext _fakeInMemoryDb;
    private IHash _HashGateway;
    private IMapper _mapper;
    private Mock<IAudit> _moqAudit;
    private ApplicationGateway _sut;
    private Establishment Establishment;
    private MultiAcademyTrust MultiAcademyTrust;

    private User User;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase("FakeInMemoryDb")
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
        var configForSmsApi = new Dictionary<string, string>
        {
            { "QueueFsmCheckStandard", "notSet" },
            { "HashCheckDays", "7" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configForSmsApi)
            .Build();
        var webJobsConnection =
            "DefaultEndpointsProtocol=https;AccountName=none;AccountKey=none;EndpointSuffix=core.windows.net";

        _moqAudit = new Mock<IAudit>(MockBehavior.Strict);
        _HashGateway = new HashGateway(new NullLoggerFactory(), _fakeInMemoryDb, _configuration, _moqAudit.Object);
        _sut = new ApplicationGateway(new NullLoggerFactory(), _fakeInMemoryDb, _mapper, _configuration);
    }

    [TearDown]
    public void Teardown()
    {
    }

    [Test]
    public void Given_DB_Add_Should_ThrowException()
    {
        // Arrange
        var db = new Mock<IEligibilityCheckContext>(MockBehavior.Strict);
        var svc = new ApplicationGateway(new NullLoggerFactory(), db.Object, _mapper, _configuration);
        db.Setup(x => x.Applications.AddAsync(It.IsAny<Application>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());
        var request = _fixture.Create<ApplicationRequestData>();

        // Act
        Func<Task> act = async () => await svc.PostApplication(request);

        // Assert
        act.Should().ThrowExactlyAsync<Exception>();
    }


    [Test]
    public async Task Given_HashNotFound_PostApplication_Should_throwException_withMessageNoCheckfound()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();
        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.eligible);

        request.ParentLastName = "";

        _fakeInMemoryDb.SaveChanges();

        // Act
        var message = $"No Check found. Type:- {request.Type}";
        Func<Task> act = async () => await _sut.PostApplication(request);

        // Assert
        act.Should().ThrowExactlyAsync<Exception>().Result.WithMessage(message);
    }

    [Test]
    public async Task Given_validRequest_PostApplication_Should_Return_ApplicationSaveFsm()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();
        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.eligible);

        // Act
        var response = _sut.PostApplication(request);

        // Assert
        response.Result.Should().BeOfType<ApplicationResponse>();
    }

    [Test]
    public async Task Given_PostApplication_InvalidEstablishment_Should_Return_Exception()
    {
        //
        await ClearDownData();
        await CreateUserEstablishmentAndLa();

        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);
        request.Establishment = -1;
        try
        {
            await _sut.PostApplication(request);
        }
        catch (Exception ex)
        {
            ex.Message.Should().BeEquivalentTo("Unable to find school:- -1, Sequence contains no elements");
        }
    }

    [Test]
    public async Task Given_PostApplication_StatusEvidenseNeeded_Should_Return_ApplicationSaveFsm()
    {
        //
        await ClearDownData();
        await CreateUserEstablishmentAndLa();

        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);

        // Act
        var response = _sut.PostApplication(request);

        // Assert
        response.Result.Should().BeOfType<ApplicationResponse>();
        response.Result.Status.Should().BeEquivalentTo(ApplicationStatus.SentForReview.ToString());
    }

    [Test]
    public void Given_InValidRequest_UpdateApplicationStatus_Should_Return_null()
    {
        // Arrange
        var guid = _fixture.Create<Guid>().ToString();
        var request = _fixture.Create<ApplicationStatusUpdateRequest>();

        // Act
        var response = _sut.UpdateApplicationStatus(guid, request.Data);

        // Assert
        response.Result.Should().BeNull();
    }

    [Test]
    public async Task Given_ValidRequest_UpdateApplicationStatus_Should_Return_UpdatedStatus()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();

        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);
        var response = _sut.PostApplication(request);

        var requestUpdateStatus = _fixture.Create<ApplicationStatusUpdateRequest>();

        // Act


        // Act
        var applicationStatusUpdate = await _sut.UpdateApplicationStatus(response.Result.Id, requestUpdateStatus.Data);

        // Assert
        applicationStatusUpdate.Should().BeOfType<ApplicationStatusUpdateResponse>();
        applicationStatusUpdate.Data.Status.Should().BeEquivalentTo(requestUpdateStatus.Data.Status.ToString());
    }


    [Test]
    public void Given_InValidRequest_GetApplication_Should_Return_null()
    {
        // Arrange
        var request = _fixture.Create<Guid>().ToString();

        // Act
        var response = _sut.GetApplication(request);

        // Assert
        response.Result.Should().BeNull();
    }

    [Test]
    public async Task Given_ValidRequest_GetApplication_Should_Return_Item()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();

        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);

        var postApplicationResponse = await _sut.PostApplication(request);

        // Act
        var response = _sut.GetApplication(postApplicationResponse.Id);

        // Assert
        response.Result.Should().BeOfType<ApplicationResponse>();
    }

    [Test]
    public void Given_NoResults_GetApplications_Should_Return_null()
    {
        // Arrange
        var requestSearch = new ApplicationSearchRequest
        {
            Data = new ApplicationSearchRequestData
            {
                ParentDateOfBirth = "1990-01-01",
                ChildDateOfBirth = "1990-01-01"
            }
        };

        // Act
        var response = _sut.GetApplications(requestSearch);

        // Assert
        response.Result.Data.Should().BeEmpty();
    }

    [Test]
    public async Task Given_ValidRequest_GetApplications_MultipleStatuses_Should_Return_results()
    {
        await ClearDownData();
        await CreateUserEstablishmentAndLa();

        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);

        var postApplicationResponse = await _sut.PostApplication(request);

        Enum.TryParse(postApplicationResponse.Status, out ApplicationStatus statusItem);

        var requestSearch = new ApplicationSearchRequest
        {
            Data = new ApplicationSearchRequestData
            {
                Statuses = new[] { statusItem },
                Establishment = Establishment.EstablishmentID
            }
        };

        // Act
        var response = await _sut.GetApplications(requestSearch);

        // Assert
        response.Data.Should().NotBeEmpty();
    }

    [Test]
    public async Task Given_ValidRequest_GetApplications_AllSearchCritieria_Should_Return_results()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();

        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);

        var postApplicationResponse = await _sut.PostApplication(request);

        Enum.TryParse(postApplicationResponse.Status, out ApplicationStatus statusItem);

        var requestSearch = new ApplicationSearchRequest
        {
            Data = new ApplicationSearchRequestData
            {
                Statuses = new[] { statusItem },
                Establishment = Establishment.EstablishmentID,
                LocalAuthority = Establishment.LocalAuthorityID,
                ParentDateOfBirth = postApplicationResponse.ParentDateOfBirth,
                ParentLastName = postApplicationResponse.ParentLastName,
                ParentNationalAsylumSeekerServiceNumber =
                    postApplicationResponse.ParentNationalAsylumSeekerServiceNumber,
                ParentNationalInsuranceNumber = postApplicationResponse.ParentNationalInsuranceNumber,
                Reference = postApplicationResponse.Reference,
                ChildDateOfBirth = postApplicationResponse.ChildDateOfBirth,
                ChildLastName = postApplicationResponse.ChildLastName
            }
        };

        // Act
        var response = await _sut.GetApplications(requestSearch);

        // Assert
        response.Data.Should().NotBeEmpty();
    }

    [Test]
    public async Task Given_ValidRequest_GetApplicationsWithNas_AllSearchCritieria_Should_Return_results()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();

        var request =
            await CreateApplicationWithNas(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);

        var postApplicationResponse = await _sut.PostApplication(request);

        Enum.TryParse(postApplicationResponse.Status, out ApplicationStatus statusItem);

        var requestSearch = new ApplicationSearchRequest
        {
            Data = new ApplicationSearchRequestData
            {
                Statuses = new[] { statusItem },
                Establishment = Establishment.EstablishmentID,
                LocalAuthority = Establishment.LocalAuthorityID,
                ParentDateOfBirth = postApplicationResponse.ParentDateOfBirth,
                ParentLastName = postApplicationResponse.ParentLastName,
                ParentNationalAsylumSeekerServiceNumber =
                    postApplicationResponse.ParentNationalAsylumSeekerServiceNumber,
                ParentNationalInsuranceNumber = postApplicationResponse.ParentNationalInsuranceNumber,
                Reference = postApplicationResponse.Reference,
                ChildDateOfBirth = postApplicationResponse.ChildDateOfBirth,
                ChildLastName = postApplicationResponse.ChildLastName
            }
        };

        // Act
        var response = await _sut.GetApplications(requestSearch);

        // Assert
        response.Data.Should().NotBeEmpty();
    }

    [Test]
    public async Task Given_ValidRequest_GetApplications_Should_Return_results()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();

        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);

        await _sut.PostApplication(request);

        var requestSearch = new ApplicationSearchRequest
        {
            Data = new ApplicationSearchRequestData
            {
                Establishment = Establishment.EstablishmentID
            }
        };

        // Act
        var response = await _sut.GetApplications(requestSearch);

        // Assert
        response.Data.Should().NotBeEmpty();
    }


    [Test]
    public async Task Given_Application_WithUserReturnNewUser()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();

        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);

        var appResponse = await _sut.PostApplication(request);

        // Act
        var response = await _sut.GetApplication(appResponse.Id);

        // Assert
        response.User.UserID.Should().BeEquivalentTo(User.UserID);
    }

    [Test]
    public async Task Given_ArchivedApplication_GetApplication_Should_ReturnOne()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();

        // Create an application
        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);
        var appResponse = await _sut.PostApplication(request);
        
        // Archive the application
        await _sut.UpdateApplicationStatus(appResponse.Id, new ApplicationStatusData
        {
            Status = ApplicationStatus.Archived
        });

        // Act
        var response = await _sut.GetApplication(appResponse.Id);

        // Assert
        response.Id.Should().Be(appResponse.Id);
        response.Status.Should().Be(ApplicationStatus.Archived.ToString());
    }

    [Test]
    public async Task Given_ExplicitSearchForArchivedStatus_GetApplications_Should_ReturnArchived()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();

        // Create and archive an application
        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);
        var appResponse = await _sut.PostApplication(request);
        
        await _sut.UpdateApplicationStatus(appResponse.Id, new ApplicationStatusData
        {
            Status = ApplicationStatus.Archived
        });

        var requestSearch = new ApplicationSearchRequest
        {
            Data = new ApplicationSearchRequestData
            {
                Establishment = Establishment.EstablishmentID,
                Statuses = new[] { ApplicationStatus.Archived }
            }
        };

        // Act
        var response = await _sut.GetApplications(requestSearch);

        // Assert
        response.TotalRecords.Should().Be(1);
        response.Data.First().Status.Should().Be(ApplicationStatus.Archived.ToString());
    }

    [Test]
    public async Task Given_ArchivedApplication_UpdateApplicationStatus_Should_UpdateSuccessfully()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();

        // Create an application
        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);
        var appResponse = await _sut.PostApplication(request);
        
        // Archive the application
        await _sut.UpdateApplicationStatus(appResponse.Id, new ApplicationStatusData
        {
            Status = ApplicationStatus.Archived
        });

        // Act - Update the archived application status to something else
        var updateResult = await _sut.UpdateApplicationStatus(appResponse.Id, new ApplicationStatusData
        {
            Status = ApplicationStatus.EvidenceNeeded
        });

        // Assert
        updateResult.Should().NotBeNull();
        updateResult.Data.Status.Should().Be("EvidenceNeeded");
    }

    [Test]
    public async Task Given_ZeroOrNegativePageNumber_GetApplications_Should_DefaultToFirstPage()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();
        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);
        await _sut.PostApplication(request);

        var requestSearch = new ApplicationSearchRequest
        {
            PageNumber = 0, // Edge case: Zero page number
            PageSize = 10, // Valid page size
            Data = new ApplicationSearchRequestData()
        };

        // Act
        var response = await _sut.GetApplications(requestSearch);

        // Assert
        response.Data.Should().NotBeEmpty(); // Ensures data is returned
        response.Data.Count().Should().BeLessOrEqualTo(10); // Ensures correct page size is respected
    }

    [Test]
    public async Task Given_NegativePageNumber_GetApplications_Should_DefaultToFirstPage()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();

        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);

        var postApplicationResponse = await _sut.PostApplication(request);
        var requestSearch = new ApplicationSearchRequest
        {
            PageNumber = -1, // Edge case: Negative page number
            PageSize = 10, // Valid page size
            Data = new ApplicationSearchRequestData()
        };

        // Act
        var response = await _sut.GetApplications(requestSearch);

        // Assert
        response.Data.Should().NotBeEmpty(); // Ensures data is returned
        response.Data.Count().Should().BeLessOrEqualTo(10); // Ensures correct page size is respected
    }

    [Test]
    public async Task Given_ValidRequest_WithMAT_GetApplications_AllSearchCritieria_Should_Return_results()
    {
        // Arrange
        await ClearDownData();
        await CreateMatAndEstablishment();

        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);

        var postApplicationResponse = await _sut.PostApplication(request);

        Enum.TryParse(postApplicationResponse.Status, out ApplicationStatus statusItem);

        var requestSearch = new ApplicationSearchRequest
        {
            Data = new ApplicationSearchRequestData
            {
                Statuses = new[] { statusItem },
                Establishment = Establishment.EstablishmentID,
                LocalAuthority = Establishment.LocalAuthorityID,
                MultiAcademyTrust = MultiAcademyTrust.MultiAcademyTrustID,
                ParentDateOfBirth = postApplicationResponse.ParentDateOfBirth,
                ParentLastName = postApplicationResponse.ParentLastName,
                ParentNationalAsylumSeekerServiceNumber =
                    postApplicationResponse.ParentNationalAsylumSeekerServiceNumber,
                ParentNationalInsuranceNumber = postApplicationResponse.ParentNationalInsuranceNumber,
                Reference = postApplicationResponse.Reference,
                ChildDateOfBirth = postApplicationResponse.ChildDateOfBirth,
                ChildLastName = postApplicationResponse.ChildLastName
            }
        };

        // Act
        var response = await _sut.GetApplications(requestSearch);

        // Assert
        response.Data.Should().NotBeEmpty();
    }

    private async Task AddHash(CheckProcessData request,
        CheckEligibilityStatus status = CheckEligibilityStatus.eligible)
    {
        _moqAudit.Setup(x => x.AuditAdd(It.IsAny<AuditData>())).ReturnsAsync("");

        var processItem = new CheckProcessData
        {
            DateOfBirth = request.DateOfBirth,
            LastName = request.LastName,
            NationalAsylumSeekerServiceNumber = request.NationalAsylumSeekerServiceNumber,
            NationalInsuranceNumber = request.NationalInsuranceNumber,
            Type = new CheckEligibilityRequestData().Type
        };
        var hashId = await _HashGateway.Create(processItem, status, ProcessEligibilityCheckSource.HO,
            new AuditData { Type = AuditType.Check });
    }

    private async Task CreateUserEstablishmentAndLa()
    {
        var la = _fixture.Create<LocalAuthority>();
        Establishment = _fixture.Create<Establishment>();
        Establishment.LocalAuthorityID = la.LocalAuthorityID;

        _fakeInMemoryDb.LocalAuthorities.Add(la);
        _fakeInMemoryDb.Establishments.Add(Establishment);
        User = _fixture.Create<User>();
        _fakeInMemoryDb.Users.Add(User);
        await _fakeInMemoryDb.SaveChangesAsync();
    }

    private async Task CreateMatAndEstablishment()
    {
        MultiAcademyTrust = _fixture.Create<MultiAcademyTrust>();
        Establishment = _fixture.Create<Establishment>();

        var multiAcademyTrustEstablishment = _fixture.Create<MultiAcademyTrustEstablishment>();
        multiAcademyTrustEstablishment.MultiAcademyTrustID = MultiAcademyTrust.MultiAcademyTrustID;
        multiAcademyTrustEstablishment.EstablishmentID = Establishment.EstablishmentID;
        MultiAcademyTrust.MultiAcademyTrustEstablishments.Add(multiAcademyTrustEstablishment);

        _fakeInMemoryDb.Establishments.Add(Establishment);
        _fakeInMemoryDb.MultiAcademyTrusts.Add(MultiAcademyTrust);
        _fakeInMemoryDb.MultiAcademyTrustEstablishments.Add(multiAcademyTrustEstablishment);
        User = _fixture.Create<User>();
        _fakeInMemoryDb.Users.Add(User);
        await _fakeInMemoryDb.SaveChangesAsync();
    }

    private async Task<ApplicationRequestData> CreateApplication(CheckEligibilityType type,
        CheckEligibilityStatus checkEligibilityStatus)
    {
        var request = _fixture.Create<ApplicationRequestData>();
        request.Type = type;
        request.ParentDateOfBirth = "1970-02-01";
        request.ChildDateOfBirth = "2007-02-01";
        request.ParentNationalAsylumSeekerServiceNumber = null;
        request.UserId = User.UserID;
        request.Establishment = Establishment.EstablishmentID;

        await AddHash(new CheckProcessData
        {
            DateOfBirth = request.ParentDateOfBirth,
            LastName = request.ParentLastName,
            Type = request.Type,
            NationalAsylumSeekerServiceNumber = request.ParentNationalAsylumSeekerServiceNumber,
            NationalInsuranceNumber = request.ParentNationalInsuranceNumber
        }, checkEligibilityStatus);
        await _fakeInMemoryDb.SaveChangesAsync();
        return request;
    }

    private async Task<ApplicationRequestData> CreateApplicationWithNas(CheckEligibilityType type,
        CheckEligibilityStatus checkEligibilityStatus)
    {
        var request = _fixture.Create<ApplicationRequestData>();
        request.Type = type;
        request.ParentDateOfBirth = "1970-02-01";
        request.ChildDateOfBirth = "2007-02-01";
        request.ParentNationalInsuranceNumber = null;
        request.UserId = User.UserID;
        request.Establishment = Establishment.EstablishmentID;

        await AddHash(new CheckProcessData
        {
            DateOfBirth = request.ParentDateOfBirth,
            LastName = request.ParentLastName,
            Type = request.Type,
            NationalAsylumSeekerServiceNumber = request.ParentNationalAsylumSeekerServiceNumber,
            NationalInsuranceNumber = request.ParentNationalInsuranceNumber
        }, checkEligibilityStatus);
        await _fakeInMemoryDb.SaveChangesAsync();
        return request;
    }

    [Test]
    public async Task Given_Application_With_Evidence_PostApplication_Should_Return_ApplicationWithEvidence()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();
        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.eligible);

        request.Evidence = new List<ApplicationEvidenceRequest>
        {
            new()
            {
                FileName = "test-document.pdf",
                FileType = "application/pdf",
                StorageAccountReference = "container/test-reference-123"
            },
            new()
            {
                FileName = "proof-of-address.jpg",
                FileType = "image/jpeg",
                StorageAccountReference = "container/test-reference-456"
            }
        };

        // Act
        var response = await _sut.PostApplication(request);

        // Assert
        response.Should().BeOfType<ApplicationResponse>();
        response.Evidence.Should().NotBeNull();
        response.Evidence.Should().HaveCount(2);
        response.Evidence.First().FileName.Should().Be("test-document.pdf");
        response.Evidence.First().FileType.Should().Be("application/pdf");
    }

    [Test]
    public async Task Given_Application_With_Evidence_GetApplication_Should_Return_EvidenceItems()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();
        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);

        request.Evidence = new List<ApplicationEvidenceRequest>
        {
            new()
            {
                FileName = "test-evidence.pdf",
                FileType = "application/pdf",
                StorageAccountReference = "container/reference-789"
            }
        };

        var createdApplication = await _sut.PostApplication(request);

        // Act
        var response = await _sut.GetApplication(createdApplication.Id);

        // Assert
        response.Should().NotBeNull();
        response.Evidence.Should().NotBeNull();
        response.Evidence.Should().HaveCount(1);
        response.Evidence.First().FileName.Should().Be("test-evidence.pdf");
        response.Evidence.First().StorageAccountReference.Should().Be("container/reference-789");
    }

    [Test]
    public async Task Given_Applications_With_Evidence_GetApplications_Should_Include_Evidence()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();
        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.notEligible);


        request.Evidence = new List<ApplicationEvidenceRequest>
        {
            new()
            {
                FileName = "search-test-doc.pdf",
                FileType = "application/pdf",
                StorageAccountReference = "container/search-reference"
            }
        };

        await _sut.PostApplication(request);

        var requestSearch = new ApplicationSearchRequest
        {
            Data = new ApplicationSearchRequestData
            {
                Establishment = Establishment.EstablishmentID
            }
        };

        // Act
        var response = await _sut.GetApplications(requestSearch);

        // Assert
        response.Data.Should().NotBeEmpty();
        response.Data.First().Evidence.Should().NotBeNull();
        response.Data.First().Evidence.Should().HaveCount(1);
        response.Data.First().Evidence.First().FileName.Should().Be("search-test-doc.pdf");
    }

    [Test]
    public async Task Given_Application_Without_Evidence_PostApplication_Should_Return_ApplicationWithEmptyEvidence()
    {
        // Arrange
        await ClearDownData();
        await CreateUserEstablishmentAndLa();
        var request = await CreateApplication(CheckEligibilityType.FreeSchoolMeals, CheckEligibilityStatus.eligible);

        request.Evidence = null;

        // Act
        var response = await _sut.PostApplication(request);

        // Assert
        response.Should().BeOfType<ApplicationResponse>();
        response.Evidence.Should().NotBeNull();
        response.Evidence.Should().BeEmpty();
    }


    private async Task ClearDownData()
    {
        _fakeInMemoryDb.Applications.RemoveRange(_fakeInMemoryDb.Applications);
        _fakeInMemoryDb.Establishments.RemoveRange(_fakeInMemoryDb.Establishments);
        _fakeInMemoryDb.LocalAuthorities.RemoveRange(_fakeInMemoryDb.LocalAuthorities);
        _fakeInMemoryDb.Users.RemoveRange(_fakeInMemoryDb.Users);
        _fakeInMemoryDb.MultiAcademyTrusts.RemoveRange(_fakeInMemoryDb.MultiAcademyTrusts);
        _fakeInMemoryDb.MultiAcademyTrustEstablishments.RemoveRange(_fakeInMemoryDb.MultiAcademyTrustEstablishments);
        _fakeInMemoryDb.SaveChanges();

        await _fakeInMemoryDb.SaveChangesAsync();
    }
}