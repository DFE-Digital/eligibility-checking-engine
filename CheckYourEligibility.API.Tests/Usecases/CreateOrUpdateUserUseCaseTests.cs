using AutoFixture;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class CreateOrUpdateUserUseCaseTests : TestBase.TestBase
{
    [SetUp]
    public void Setup()
    {
        _mockUserGateway = new Mock<IUsers>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new CreateOrUpdateUserUseCase(_mockUserGateway.Object, _mockAuditGateway.Object);
    }

    [TearDown]
    public void Teardown()
    {
        _mockUserGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<IUsers> _mockUserGateway;
    private Mock<IAudit> _mockAuditGateway;
    private CreateOrUpdateUserUseCase _sut;

    [Test]
    public async Task Execute_Should_Return_UserSaveItemResponse_When_Successful()
    {
        // Arrange
        var request = _fixture.Create<UserCreateRequest>();
        var responseId = _fixture.Create<string>();

        _mockUserGateway.Setup(us => us.Create(request.Data)).ReturnsAsync(responseId);
        _mockAuditGateway.Setup(a => a.CreateAuditEntry(AuditType.User, responseId, null))
            .ReturnsAsync(_fixture.Create<string>());

        var expectedResponse = new UserSaveItemResponse { Data = responseId };

        // Act
        var result = await _sut.Execute(request);

        // Assert
        result.Should().BeEquivalentTo(expectedResponse);
    }
}