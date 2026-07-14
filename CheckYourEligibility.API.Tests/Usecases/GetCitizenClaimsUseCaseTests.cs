using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Constants;
using CheckYourEligibility.Core.UseCases;
using FluentAssertions;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class GetCitizenClaimsUseCaseTests : TestBase
{
    [SetUp]
    public void Setup()
    {
        _sut = new GetCitizenClaimsUseCase();
    }

    private GetCitizenClaimsUseCase _sut;

    [Test]
    public async Task Execute_Should_Return_DwpClaimsResponse_When_Guid_Is_Valid_And_Eligible()
    {
        // Arrange
        var guid = MogDWPValues.validCitizenEligibleGuid;
        var benefitType = "pensions_credit";
        var expectedJson = @"{
                                    'jsonapi': {
                                        'version': '1.0'
                                    },
                                    'data': [
                                        {
                                            'attributes': {
                                                'benefitType': 'pensions_credit',
                                                'awards': [
                                                    {
                                                        'amount': 1111111,
                                                        'endDate': '2029-06-05',
                                                        'endReason': 'customer_required_to_be_available_for_work',
                                                        'startDate': '2018-05-06',
                                                        'status': 'cacs_decision_hist'
                                                    }
                                                ],
                                                'guid': 'e0f4569cb36590fab2c8cb3a861b4c68fae2aac75ff7b70495b60cdb7679fffe',
                                                'startDate': '2018-05-02',
                                                'decisionDate': '2018-05-03',
                                                'status': 'decision_entitled'
                                            },
                                            'id': '80',
                                            'type': 'Claim'
                                        }
                                    ],
                                    'links': {
                                        'self': 'https://nhs-test.integr-dev.dwpcloud.uk:8443/capi/v2/citizens/90d1732b81e36b870d14fe4a9994a9d2a6f7e7fc44287f3bd8e8b41fc727327a/claims'
                                    }
                                }";
        var expectedResponse = JsonConvert.DeserializeObject<DwpClaimsResponse>(expectedJson);

        // Act
        var result = await _sut.Execute(guid, benefitType);

        // Assert
        result.Should().BeEquivalentTo(expectedResponse);
    }

    [Test]
    public async Task Execute_Should_Return_Null_When_Guid_Is_Valid_And_Not_Eligible()
    {
        // Arrange
        var guid = MogDWPValues.validCitizenNotEligibleGuid;
        var benefitType = "pensions_credit";

        // Act
        var result = await _sut.Execute(guid, benefitType);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void Execute_Should_Throw_ArgumentException_When_Guid_Is_Invalid()
    {
        // Arrange
        var guid = "invalid_guid";
        var benefitType = "pensions_credit";

        // Act
        Func<Task> act = async () => await _sut.Execute(guid, benefitType);

        // Assert
        act.Should().ThrowAsync<ArgumentException>().WithMessage("Invalid GUID");
    }
}