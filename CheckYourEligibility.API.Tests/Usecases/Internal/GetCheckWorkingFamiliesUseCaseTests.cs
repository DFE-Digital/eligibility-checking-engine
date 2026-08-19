using AutoFixture;
using CheckYourEligibility.API.Boundary.Responses.Internal;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Services;
using CheckYourEligibility.API.UseCases.Internal;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases.Internal
{
    [TestFixture]
    public class GetCheckWorkingFamiliesUseCaseTests : TestBase.TestBase
    {
        private Mock<IEligibilityCheckDataResponseMapper> _mapper;
        private Mock<ICheckEligibility> _checkGateway;
        private Mock<ILogger<GetEligibilityCheckItemUseCase>> _logger;

        private GetCheckWorkingFamiliesItemUseCase _sut;

        [SetUp]
        public void Setup()
        {
            _mapper = new Mock<IEligibilityCheckDataResponseMapper>(MockBehavior.Strict);
            _checkGateway = new Mock<ICheckEligibility>(MockBehavior.Strict);
            _logger = new Mock<ILogger<GetEligibilityCheckItemUseCase>>();

            _sut = new GetCheckWorkingFamiliesItemUseCase(
                _mapper.Object,
                _logger.Object,
                _checkGateway.Object);
        }

        [Test]
        public void Execute_throws_validation_exception_when_guid_is_null()
        {
            // Act
            Func<Task> act = async () =>
                await _sut.Execute(null, DateTime.Today);

            // Assert
            act.Should().ThrowAsync<ValidationException>()
                .WithMessage("*check ID is required*");
        }

        [Test]
        public void Execute_throws_not_found_when_check_not_found()
        {
            // Arrange
            var guid = _fixture.Create<string>();

            _checkGateway
                .Setup(x => x.GetItem(guid))
                .ReturnsAsync((EligibilityCheck)null);

            // Act
            Func<Task> act = async () =>
                await _sut.Execute(guid, DateTime.Today);

            // Assert
            act.Should().ThrowAsync<NotFoundException>();

            _checkGateway.Verify(x => x.GetItem(guid), Times.Once);
        }
        [Test]
        public async Task Execute_returns_response_when_check_exists()
        {
            // Arrange
            var guid = _fixture.Create<string>();
            var checkDate = DateTime.Today;

            var eligibilityCheck = _fixture.Create<EligibilityCheck>();

            var mappedItem = _fixture.Build<CheckEligibilityWorkingFamiliesItem>()
                .With(x => x.EligibilityCode, "50012345678")
                .Create();

            _checkGateway
                .Setup(x => x.GetItem(guid))
                .ReturnsAsync(eligibilityCheck);

            _mapper
                .Setup(x => x.MapCheckDataToResponseWorkingFamilies(eligibilityCheck,true))
                .Returns(mappedItem);

            // Act
            var result = await _sut.Execute(guid, checkDate);

            // Assert
            _mapper.Verify(
                x => x.MapCheckDataToResponseWorkingFamilies(eligibilityCheck, true),
                Times.Once);
            result.Should().NotBeNull();
            result.Data.Should().NotBeNull();

            result.Links.Should().NotBeNull();

            result.Links.Get_EligibilityCheck
                .Should().Be($"{CheckLinks.InternalWorkingFamiliesGetLink}{guid}");

            result.Links.Put_EligibilityCheckProcess
                .Should().Be($"{CheckLinks.ProcessLink}{guid}");

            result.Links.Get_EligibilityCheckStatus
                .Should().Be($"{CheckLinks.GetLink}{guid}/Status");
        }
        [Test]
        public async Task Execute_sets_working_family_properties()
        {
            // Arrange
            var guid = _fixture.Create<string>();
            var checkDate = new DateTime(2025, 6, 1);

            var eligibilityCheck = _fixture.Create<EligibilityCheck>();
            var mappedItem = new CheckEligibilityWorkingFamiliesItem
            {
                EligibilityCode = "50012345678",
                ValidityStartDate = "2025-01-01",
                DiscretionaryValidityStartDate = "2025-01-01",
                ValidityEndDate = "2025-12-31",
                GracePeriodEndDate = "2026-03-31",
                DateOfBirth = "2022-01-01"
            };

            _checkGateway
                .Setup(x => x.GetItem(guid))
                .ReturnsAsync(eligibilityCheck);

            _mapper
                .Setup(x => x.MapCheckDataToResponseWorkingFamilies(eligibilityCheck, true))
                .Returns(mappedItem);

            // Act
            var result = await _sut.Execute(guid, checkDate);

            // Assert
            result.Data.IsDiscretionaryValidityStartDateApplied.Should().NotBeNull();
            result.Data.EligibilityCodeType.Should().NotBeNull();
            result.Data.TermValidity.Should().NotBeNull();
            result.Data.ReconfirmationProperties.Should().NotBeNull();
        }

    }
}
