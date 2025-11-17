using AutoFixture;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class RestoreArchivedApplicationStatusUseCaseTests
{
    [SetUp]
    public void Setup()
    {
        _mockApplicationGateway = new Mock<IApplication>(MockBehavior.Strict);
        _mockAuditGateway = new Mock<IAudit>(MockBehavior.Strict);
        _sut = new RestoreArchivedApplicationStatusUseCase(_mockApplicationGateway.Object, _mockAuditGateway.Object);
        _fixture = new Fixture();
    }

    [TearDown]
    public void Teardown()
    {
        _mockApplicationGateway.VerifyAll();
        _mockAuditGateway.VerifyAll();
    }

    private Mock<IApplication> _mockApplicationGateway = null!;
    private Mock<IAudit> _mockAuditGateway = null!;
    private RestoreArchivedApplicationStatusUseCase _sut = null!;
    private Fixture _fixture = null!;

    
}