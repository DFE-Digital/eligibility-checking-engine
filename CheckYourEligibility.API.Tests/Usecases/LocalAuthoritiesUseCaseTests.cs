using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.Usecases;
using Moq;

namespace CheckYourEligibility.API.Tests.Usecases
{
    [TestFixture]
    public class LocalAuthoritiesUseCaseTests
    {
        private Mock<ILocalAuthority> _localAuthorityGatewayMock;
        private Mock<IEligibilityPolicy> _eligibilityPolicyMock;
        private LocalAuthoritiesUseCase _localAuthoritiesUseCase;

        [SetUp]
        public void SetUp()
        {
            _localAuthorityGatewayMock = new Mock<ILocalAuthority>();
            _eligibilityPolicyMock = new Mock<IEligibilityPolicy>();
            _localAuthoritiesUseCase = new LocalAuthoritiesUseCase(_localAuthorityGatewayMock.Object, _eligibilityPolicyMock.Object);
        }

        [Test]
        public void Execute_WhenLocalAuthorityNotFound_ThrowsNotFoundException()
        {
            _localAuthorityGatewayMock.Setup(x => x.GetLocalAuthorityById(It.IsAny<int>(), null)).ReturnsAsync((LocalAuthority)null);
            Assert.ThrowsAsync<NotFoundException>(() => _localAuthoritiesUseCase.Execute(123));
        }

        [Test]
        public async Task Execute_ShouldReturn_Standard_ForTwoYearPolicyPolicy_And_MockedPolicies()
        {
            var la = new LocalAuthority
            {
                LocalAuthorityID = 123,
                FreeSchoolMealsPolicyID = 4,
                EarlyYearsPupilPremiumPolicyID = 2,
                TwoYearPolicyID = 0,
                SchoolCanReviewEvidence = true
            };
            _localAuthorityGatewayMock.Setup(x => x.GetLocalAuthorityById(la.LocalAuthorityID,null)).ReturnsAsync(la);

            _eligibilityPolicyMock.Setup(x => x.GeEligibilityPolicyByIdAsync(la.FreeSchoolMealsPolicyID,null)).ReturnsAsync(new EligibilityPolicy
            {
                CheckType = CheckEligibilityType.FreeSchoolMeals,
                EligibilityCriteria = EligibilityCriteria.expanded,
            });
            _eligibilityPolicyMock.Setup(x => x.GeEligibilityPolicyByIdAsync(la.EarlyYearsPupilPremiumPolicyID,null)).ReturnsAsync(new EligibilityPolicy
            {
                CheckType = CheckEligibilityType.EarlyYearPupilPremium,
                EligibilityCriteria = EligibilityCriteria.standard
            });

            var result = await _localAuthoritiesUseCase.Execute(la.LocalAuthorityID);

            Assert.That(result.SchoolCanReviewEvidence, Is.True);

            Assert.That(result.EligibilityPolicies.Count, Is.EqualTo(3));

            Assert.That(result.EligibilityPolicies[0].CheckType, Is.EqualTo("FreeSchoolMeals"));
            Assert.That(result.EligibilityPolicies[0].EligibilityCriteria, Is.EqualTo("expanded"));

            Assert.That(result.EligibilityPolicies[1].CheckType, Is.EqualTo("EarlyYearPupilPremium"));
            Assert.That(result.EligibilityPolicies[1].EligibilityCriteria, Is.EqualTo("standard"));

            Assert.That(result.EligibilityPolicies[2].CheckType, Is.EqualTo("TwoYearOffer"));
            Assert.That(result.EligibilityPolicies[2].EligibilityCriteria, Is.EqualTo("standard"));

        }
    }
}
