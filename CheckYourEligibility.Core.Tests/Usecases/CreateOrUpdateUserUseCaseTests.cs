using AutoFixture;
using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Enums;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.Core.Tests.UseCases;

[TestFixture]
public class CreateOrUpdateUserUseCaseTests : TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockUserGateway = new Mock<IUsers>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new CreateOrUpdateFSMParentUserUseCase(_mockUserGateway.Object, _mockAuditGateway.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockUserGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<IUsers> _mockUserGateway;
    private Mock<IAudit> _mockAuditGateway;
    private CreateOrUpdateFSMParentUserUseCase _sut;

    [Test]
    public async Task Execute_Should_Return_UserSaveItemResponse_When_Successful()
    {
        // Arrange
        var request = _fixture.Create<UserCreateRequest>();
        var responseId = _fixture.Create<string>();

        _mockUserGateway.Setup(us =>  us.CreateOrUpdateFSMParentUser(request)).ReturnsAsync(responseId);

        var expectedResponse = new UserSaveItemResponse { Data = responseId };

        // Act
        var result = await _sut.Execute(request);

        // Assert
        result.Should().BeEquivalentTo(expectedResponse);
    }
}