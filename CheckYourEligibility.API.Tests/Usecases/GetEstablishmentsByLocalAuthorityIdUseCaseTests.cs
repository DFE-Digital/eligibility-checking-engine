
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases;

[TestFixture]
public class GetEstablishmentsByLocalAuthorityIdUseCaseTests : TestBase.TestBase
{
    private Mock<ILocalAuthority> _mockGateway;
    private Mock<ILogger<GetEstablishmentsByLocalAuthorityIdUseCase>> _mockLogger;
    private GetEstablishmentsByLocalAuthorityIdUseCase _sut;

    [SetUp]
    public void Setup()
    {
        _mockGateway = new Mock<ILocalAuthority>();
        _mockLogger = new Mock<ILogger<GetEstablishmentsByLocalAuthorityIdUseCase>>();

        _sut = new GetEstablishmentsByLocalAuthorityIdUseCase(
            _mockGateway.Object,
            _mockLogger.Object
        );
    }

    [Test]
    public void Execute_InvalidLocalAuthorityId_ThrowsNotFoundException()
    {
        // Arrange
        var invalidId = 0;

        // Act + Assert
        var ex = Assert.ThrowsAsync<NotFoundException>(
            async () => await _sut.Execute(invalidId)
        );

        Assert.That(ex.Message, Is.EqualTo("Invalid local authority"));
    }

    [Test]
    public async Task Execute_ValidId_CallsGatewayAndReturnsResponse()
    {
        // Arrange
        var laId = 123;

        var establishments = new List<EstablishmentResponseItem>
        {
            new EstablishmentResponseItem
            {
                URN = 1,
                Name = "Test School"
            }
        };

        _mockGateway
            .Setup(x => x.GetEstablishmentsByLocalAuthorityId(laId))
            .ReturnsAsync(establishments);

        // Act
        var result = await _sut.Execute(laId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Data.Count, Is.EqualTo(1));
        Assert.That(result.Data.First().Name, Is.EqualTo("Test School"));

        _mockGateway.Verify(
            x => x.GetEstablishmentsByLocalAuthorityId(laId),
            Times.Once
        );
    }

    [Test]
    public async Task Execute_WhenGatewayReturnsEmptyList_ReturnsEmptyResponse()
    {
        // Arrange
        var laId = 456;

        _mockGateway
            .Setup(x => x.GetEstablishmentsByLocalAuthorityId(laId))
            .ReturnsAsync(new List<EstablishmentResponseItem>());

        // Act
        var result = await _sut.Execute(laId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Data, Is.Empty);
    }
}