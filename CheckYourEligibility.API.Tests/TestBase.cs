using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Idioms;
using Moq;

namespace CheckYourEligibility.TestBase;

[ExcludeFromCodeCoverage]
public abstract class TestBase
{
    protected readonly Fixture _fixture = new();
    protected readonly MockRepository MockRepository = new(MockBehavior.Strict);
    private Stopwatch _stopwatch;

    [SetUp]
    public void TestInitialize()
    {
        // Configure fixture to handle circular references automatically
        // This prevents AutoFixture.ObjectCreationExceptionWithPath due to circular references
        // between domain entities like EligibilityCheck <-> BulkCheck
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _stopwatch = Stopwatch.StartNew();
    }

    [TearDown]
    public void TestCleanup()
    {
        MockRepository.VerifyAll();
        _stopwatch.Stop();

        static void lineBreak()
        {
            Trace.WriteLine(new string('*', 50));
        }

        lineBreak();
        Trace.WriteLine(string.Format("* Elapsed time for test (milliseconds): {0}",
            _stopwatch.Elapsed.TotalMilliseconds));
        lineBreak();
    }

    protected void RunGuardClauseConstructorTest<T>()
    {
        // Arrange
        var fixture = new Fixture().Customize(new AutoMoqCustomization());
        // Configure the local fixture for circular references as well
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        // Act & Assert
        var assertion = new GuardClauseAssertion(fixture);
        assertion.Verify(typeof(T).GetConstructors());
    }
}