using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Usecases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Tests.Usecases
{
    [TestFixture]
    public class GetEligibilityCheckReportItemsUseCaseTests : TestBase.TestBase
    {
        private Mock<IEligibilityCheckReporting> _mockReportingGateway;
        private Mock<ILogger<GetEligibilityCheckReportItemsUseCase>> _mockLogger;
        private GetEligibilityCheckReportItemsUseCase _sut;

        [SetUp]
        public void SetUp()
        {
            _mockReportingGateway = new Mock<IEligibilityCheckReporting>(MockBehavior.Strict);
            _mockLogger = new Mock<ILogger<GetEligibilityCheckReportItemsUseCase>>();
            _sut = new GetEligibilityCheckReportItemsUseCase(_mockReportingGateway.Object, _mockLogger.Object);
        }

        [Test]
        public void Execute_ThrowsValidationException_WhenReportIdIsInvalid()
        {
            // Act
            Func<Task> act = async () => await _sut.Execute("not-a-guid");
            // Assert
            act.Should().ThrowAsync<ValidationException>().WithMessage("Invalid report ID format. Must be a GUID");
        }

        [Test]
        public async Task Execute_ThrowsNotFoundException_WhenReportNotFound()
        {
            // Arrange
            var guid = Guid.NewGuid();
            _mockReportingGateway.Setup(x => x.GetEligibilityReportById(guid)).ReturnsAsync((EligibilityCheckReport)null);
            // Act
            Func<Task> act = async () => await _sut.Execute(guid.ToString());
            // Assert
            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Execute_ReturnsEmptyResponse_WhenNoReportItems()
        {
            // Arrange
            var guid = Guid.NewGuid();

            _mockReportingGateway
                    .Setup(x => x.GetEligibilityReportById(guid))
                    .ReturnsAsync(new EligibilityCheckReport());

            _mockReportingGateway.Setup(x => x.GetEligibilityChecksByReportId(guid)).ReturnsAsync(new Dictionary<Guid, EligibilityCheck> { });
            // Act
            var result = await _sut.Execute(guid.ToString());
            // Assert
            result.Data.Should().BeEmpty();
        }

        [Test]
        public async Task Execute_ReturnsMappedItems_WhenReportItemsExist()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            string checkItem1Id = Guid.NewGuid().ToString();
            string checkItem2Id = Guid.NewGuid().ToString();
            string checkItem3Id = Guid.NewGuid().ToString();

            var checkData = new CheckProcessData { LastName = "Smith", NationalInsuranceNumber = "AB123456C", DateOfBirth = "2000-01-01" };
            var eligibilityCheck = new EligibilityCheck
            {
                EligibilityCheckID = checkItem1Id,
                CheckData = JsonConvert.SerializeObject(checkData),
                Created = new DateTime(2024, 1, 1),
                Status = CheckEligibilityStatus.eligible,
                Tier = null,
                Type = CheckEligibilityType.FreeSchoolMeals,
                UserName = "tester-1"
            };

            var checkData2 = new CheckProcessData { LastName = "Wilson", NationalInsuranceNumber = "AB123456A", DateOfBirth = "2000-01-01" };
            var eligibilityCheckWithTargetedTier = new EligibilityCheck
            {
                EligibilityCheckID = checkItem2Id,
                CheckData = JsonConvert.SerializeObject(checkData2),
                Created = new DateTime(2024, 1, 1),
                Status = CheckEligibilityStatus.eligible,
                Tier = EligibilityTier.targeted,
                Type = CheckEligibilityType.FreeSchoolMeals,
                UserName = "tester-2"
            };

            var checkData3 = new CheckProcessData { LastName = "Brown", NationalInsuranceNumber = "AB123456B", DateOfBirth = "2000-01-01" };
            var eligibilityCheckWithExpandedTier = new EligibilityCheck
            {
                EligibilityCheckID = checkItem3Id,
                CheckData = JsonConvert.SerializeObject(checkData3),
                Created = new DateTime(2024, 1, 1),
                Status = CheckEligibilityStatus.eligible,
                Tier = EligibilityTier.expanded,
                Type = CheckEligibilityType.FreeSchoolMeals,
                UserName = "tester-3"
            };

            _mockReportingGateway.Setup(x => x.GetEligibilityReportById(reportId)).ReturnsAsync(new EligibilityCheckReport());
            _mockReportingGateway.Setup(x => x.GetEligibilityChecksByReportId(reportId)).ReturnsAsync(new Dictionary<Guid, EligibilityCheck>
            {
                { Guid.Parse(checkItem1Id), eligibilityCheck },
                { Guid.Parse(checkItem2Id), eligibilityCheckWithTargetedTier },
                { Guid.Parse(checkItem3Id), eligibilityCheckWithExpandedTier }
            });

            // Act
            var result = await _sut.Execute(reportId.ToString());

            // Assert
            result.Data.Should().BeEquivalentTo(new[]
            {
            new
            {
                ParentName = "Smith",
                NationalInsuranceNumber = "AB123456C",
                DateOfBirth = "2000-01-01",
                CheckSubmittedDate = "2024-01-01",
                Outcome = "eligible",
                Tier = "N/A",
                CheckType = "FreeSchoolMeals",
                CheckedBy = "tester-1"
            },
            new
            {
                ParentName = "Wilson",
                NationalInsuranceNumber = "AB123456A",
                DateOfBirth = "2000-01-01",
                CheckSubmittedDate = "2024-01-01",
                Outcome = "eligible",
                Tier = "Eligible targeted",
                CheckType = "FreeSchoolMeals",
                CheckedBy = "tester-2"
            },
            new
            {
                ParentName = "Brown",
                NationalInsuranceNumber = "AB123456B",
                DateOfBirth = "2000-01-01",
                CheckSubmittedDate = "2024-01-01",
                Outcome = "eligible",
                Tier = "Eligible expanded",
                CheckType = "FreeSchoolMeals",
                CheckedBy = "tester-3"
            }
        }, options => options.WithoutStrictOrdering());
        }
    }
}
