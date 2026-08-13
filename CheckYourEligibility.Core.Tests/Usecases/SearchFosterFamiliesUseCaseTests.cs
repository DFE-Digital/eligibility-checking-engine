using System.ComponentModel.DataAnnotations;
using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain.Constants.ErrorMessages;
using CheckYourEligibility.Core.Gateways.Interfaces;
using CheckYourEligibility.Core.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.Core.Tests.UseCases;

[TestFixture]
public class SearchFosterFamiliesUseCaseTests
{
    private Mock<IFosterFamilies> _gateway;
    private SearchFosterFamiliesUseCase _sut;

    [SetUp]
    public void Setup()
    {
        _gateway = new Mock<IFosterFamilies>();
        _sut = new SearchFosterFamiliesUseCase(_gateway.Object);
    }

    [Test]
    public async Task Execute_ShouldThrowArgumentNullException_WhenRequestIsNull()
    {
        // Arrange
        
        // Act
        var act = () => _sut.Execute(null!, 1);

        // Assert
        await FluentActions
            .Invoking(act)
            .Should()
            .ThrowAsync<ArgumentNullException>();
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-10)]
    public async Task Execute_ShouldThrowValidationException_WhenPageNumberIsInvalid(
        int pageNumber)
    {
        // Arrange
        var request = new FosterFamiliesSearchRequest
        {
            PageNumber = pageNumber,
            PageSize = 10
        };

        // Act
        var act = () => _sut.Execute(request, 1);

        // Assert
        var ex = await FluentActions
            .Invoking(act)
            .Should()
            .ThrowAsync<ValidationException>();

        ex.And.Message.Should().Be(FosterFamilyValidationMessages.InvalidPageNumber);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(11)]
    [TestCase(20)]
    public async Task Execute_ShouldThrowValidationException_WhenPageSizeIsInvalid(
        int pageSize)
    {
        // Arrange
        var request = new FosterFamiliesSearchRequest
        {
            PageNumber = 1,
            PageSize = pageSize
        };

        // Act
        var act = () => _sut.Execute(request, 1);

        // Assert
        var ex = await FluentActions
            .Invoking(act)
            .Should()
            .ThrowAsync<ValidationException>();

        ex.And.Message.Should().Be(FosterFamilyValidationMessages.InvalidPageSize);
    }

    [Test]
    public async Task Execute_ShouldCallGateway_WithCorrectParameters()
    {
        // Arrange
        var request = new FosterFamiliesSearchRequest
        {
            PageNumber = 1,
            PageSize = 10
        };

        var response = new FosterFamiliesSearchResponse();

        _gateway
            .Setup(x => x.SearchFosterFamilies(123, request))
            .ReturnsAsync(response);

        // Act
        await _sut.Execute(request, 123);

        // Assert
        _gateway.Verify(
            x => x.SearchFosterFamilies(123, request),
            Times.Once);
    }

    [Test]
    public async Task Execute_ShouldReturnGatewayResponse_WhenGatewayReturnsResponse()
    {
        // Arrange
        var request = new FosterFamiliesSearchRequest
        {
            PageNumber = 1,
            PageSize = 10
        };

        var expectedResponse = new FosterFamiliesSearchResponse
        {
            Data = new[]
            {
                new FosterFamiliesSearchItemResponse()
            }
        };

        _gateway
            .Setup(x => x.SearchFosterFamilies(It.IsAny<int>(), request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _sut.Execute(request, 1);

        // Assert
        result.Should().BeSameAs(expectedResponse);
    }

    [Test]
    public async Task Execute_ShouldReturnEmptyResponse_WhenGatewayReturnsNull()
    {
        // Arrange
        var request = new FosterFamiliesSearchRequest
        {
            PageNumber = 1,
            PageSize = 10
        };

        _gateway
            .Setup(x => x.SearchFosterFamilies(It.IsAny<int>(), request))
            .ReturnsAsync((FosterFamiliesSearchResponse)null!);

        // Act
        var result = await _sut.Execute(request, 1);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().NotBeNull();
        result.Data.Should().BeEmpty();
    }
}