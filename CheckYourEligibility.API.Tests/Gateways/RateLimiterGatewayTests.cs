using System.Threading.Tasks;
using AutoFixture;
using AutoMapper;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Data.Mappings;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CheckYourEligibility.API.Tests;

public class RateLimiterServiceTests : TestBase.TestBase
{
    private IEligibilityCheckContext _fakeInMemoryDb;
    private RateLimitGateway _sut;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<EligibilityCheckContext>()
            .UseInMemoryDatabase("FakeInMemoryDb")
            .Options;

        _fakeInMemoryDb = new EligibilityCheckContext(options);

        _sut = new RateLimitGateway(new NullLoggerFactory(), _fakeInMemoryDb);
    }

    [TearDown]
    public void Teardown()
    {
    }

    [Test]
    public async Task Given_Create_Should_Return_Pass()
    {
        // Arrange
        var request = _fixture.Create<RateLimitEvent>();
        // Act
        await _sut.Create(request);
        // Assert
        Assert.Pass();
    }

    [Test]
    public async Task Given_Existing_Event_Update_Return_Pass()
    {
        /*
        // Arrange
        var request = _fixture.Create<RateLimitEvent>();
        // Act
        await _sut.UpdateStatus(request.RateLimitEventId, true);
        // Assert
        Assert.Pass();
        */
    }

    [Test]
    public async Task Given_NonExisting_Event_Update_Throws_Exception()
    {
        //TODO: Implement test logic
    }

    [Test]
    public async Task Given_No_Existing_Event_Get_Query_Size_Is_Zero()
    {
        //TODO: Implement test logic
    }

    [Test]
    public async Task Given_No_Existing_Event_Get_Query_Size_Returns_Sum()
    {
        //TODO: Implement test logic
    }
}