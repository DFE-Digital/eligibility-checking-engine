using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class GetEstablishmentsByMultiAcademyTrustIdUseCaseTests : TestBase.TestBase
{
    private Mock<IMultiAcademyTrust> _mockGateway;
    private Mock<ILogger<GetEstablishmentsByMultiAcademyTrustIdIdUseCase>> _mockLogger;
    private GetEstablishmentsByMultiAcademyTrustIdIdUseCase _sut;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<IMultiAcademyTrust>();
        _mockLogger = new Mock<ILogger<GetEstablishmentsByMultiAcademyTrustIdIdUseCase>>();

        _sut = new GetEstablishmentsByMultiAcademyTrustIdIdUseCase(
            _mockGateway.Object,
            _mockLogger.Object
        );
    }

    [Test]
    public void Execute_InvalidMultiAcademyTrustId_ThrowsNotFoundException()
    {
        // Arrange
        var invalidId = 0;

        // Act + Assert
        var ex = Assert.ThrowsAsync<NotFoundException>(
            async () => await _sut.Execute(invalidId)
        );

        Assert.That(ex.Message, Is.EqualTo("Invalid Multi Academy Trust"));
    }

    [Test]
    public async Task Execute_ValidId_CallsGatewayAndReturnsResponse()
    {
        // Arrange
        var matId = 123;

        var establishments = new List<EstablishmentResponseItem>
        {
            new EstablishmentResponseItem
            {
                URN = 1,
                Name = "Test School"
            }
        };

        _mockGateway
            .Setup(x => x.GetEstablishmentsByMultiAcademyTrustId(matId))
            .ReturnsAsync(establishments);

        // Act
        var result = await _sut.Execute(matId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Data.Count, Is.EqualTo(1));
        Assert.That(result.Data.First().Name, Is.EqualTo("Test School"));

        _mockGateway.Verify(
            x => x.GetEstablishmentsByMultiAcademyTrustId(matId),
            Times.Once
        );
    }

    [Test]
    public async Task Execute_WhenGatewayReturnsEmptyList_ReturnsEmptyResponse()
    {
        // Arrange
        var matId = 456;

        _mockGateway
            .Setup(x => x.GetEstablishmentsByMultiAcademyTrustId(matId))
            .ReturnsAsync(new List<EstablishmentResponseItem>());

        // Act
        var result = await _sut.Execute(matId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Data, Is.Empty);
    }
}