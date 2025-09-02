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
    public async Task Given_Create_Should_Store_Event()
    {
        // Arrange
        var guid = _fixture.Create<Guid>().ToString();
        var checkEvent = _fixture.Create<RateLimitEvent>();
        checkEvent.RateLimitEventId = guid;
        // Act
        await _sut.Create(checkEvent);
        // Assert
        _fakeInMemoryDb.RateLimitEvents.Find(guid).Should().Be(checkEvent);
    }

    [Test]
    public void Given_Existing_Event_Update_Return_Pass()
    {
        // Arrange
        var guid = _fixture.Create<Guid>().ToString();
        var mockEvent = _fixture.Create<RateLimitEvent>();
        mockEvent.RateLimitEventId = guid;
        mockEvent.Accepted = true;
        _fakeInMemoryDb.RateLimitEvents.Add(mockEvent);
        _fakeInMemoryDb.SaveChangesAsync();
        // Act
        var response = _sut.UpdateStatus(guid, false);
        // Assert
        response.Should().Be(Task.CompletedTask);
        _fakeInMemoryDb.RateLimitEvents.Find(guid)?.Accepted.Should().BeFalse();
    }

    [Test]
    public void Given_NonExisting_Event_Update_Should_Return_Null()
    {
        // Arrange
        var guid = _fixture.Create<Guid>().ToString();

        // Act
        var response = _sut.UpdateStatus(guid, false);

        // Assert
        response.Should().Be(Task.CompletedTask);
        _fakeInMemoryDb.RateLimitEvents.Find(guid).Should().BeNull();

    }

    [Test]
    public async Task Given_No_Existing_Event_Get_QueriesInWindow_Is_Zero()
    {
        // Act
        var response = await _sut.GetQueriesInWindow("empty-partition", DateTime.UtcNow, TimeSpan.FromHours(1));

        // Assert
        response.Should().Be(0);
    }

    [Test]
    public async Task Given_No_Existing_Event_Get_Query_Size_Returns_Sum()
    {
        // Arrange
        string _partitionName = "test-partition";
        var checkEvent = _fixture.Create<RateLimitEvent>();
        checkEvent.QuerySize = 1;
        checkEvent.PartitionName = _partitionName;
        checkEvent.TimeStamp = DateTime.UtcNow.AddMinutes(-30);
        var bulkEvent = _fixture.Create<RateLimitEvent>();
        bulkEvent.QuerySize = 4;
        bulkEvent.PartitionName = _partitionName;
        bulkEvent.TimeStamp = DateTime.UtcNow.AddMinutes(-20);

        _fakeInMemoryDb.RateLimitEvents.Add(checkEvent);
        _fakeInMemoryDb.RateLimitEvents.Add(bulkEvent);
        await _fakeInMemoryDb.SaveChangesAsync();

        // Act
        var response = await _sut.GetQueriesInWindow(_partitionName, DateTime.UtcNow, TimeSpan.FromHours(1));

        // Assert
        response.Should().Be(5);
    }

    [Test]
    public async Task Given_CleanUpRateLimitEvents_Should_Return_Pass()
    {
        // Arrange

        // Act
        await _sut.CleanUpRateLimitEvents();

        // Assert
        Assert.Pass();
    }

    [Test]
    public async Task Given_CleanUpRateLimitEvents_Should_Remove_Old_Events()
    {
        // Arrange
        var checkEvent = _fixture.Create<RateLimitEvent>();
        checkEvent.TimeStamp = DateTime.UtcNow.AddDays(-10);
        _fakeInMemoryDb.RateLimitEvents.Add(checkEvent);
        _fakeInMemoryDb.SaveChanges();

        // Act
        await _sut.CleanUpRateLimitEvents();

        // Assert
        _fakeInMemoryDb.RateLimitEvents.Count().Should().Be(0);
    }
    
    [Test]
    public async Task Given_CleanUpRateLimitEvents_Should_Keep_Current_Events()
    {
        // Arrange
        var checkEvent = _fixture.Create<RateLimitEvent>();
        checkEvent.TimeStamp = DateTime.UtcNow.AddDays(-6);
        _fakeInMemoryDb.RateLimitEvents.Add(checkEvent);
        _fakeInMemoryDb.SaveChanges();

        // Act
        await _sut.CleanUpRateLimitEvents();

        // Assert
        _fakeInMemoryDb.RateLimitEvents.Count().Should().Be(1);
    }
}