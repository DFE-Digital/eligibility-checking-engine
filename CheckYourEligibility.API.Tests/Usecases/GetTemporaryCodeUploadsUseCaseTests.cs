using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using FluentAssertions;
using Moq;

namespace CheckYourEligibility.API.Tests.UseCases
{

	[TestFixture]
	public class GetTemporaryCodeUploadsUseCaseTests
	{
		private Mock<ITemporaryCodeUpload> _mockGateway = null!;
		private GetTemporaryCodeUploadsUseCase _sut = null!;

		[SetUp]
		public void Setup()
		{
			_mockGateway = new Mock<ITemporaryCodeUpload>(MockBehavior.Strict);

			_sut = new GetTemporaryCodeUploadsUseCase(
				_mockGateway.Object);
		}

		[TearDown]
		public void TearDown()
		{
			_mockGateway.VerifyAll();
		}

		[Test]
		public async Task Execute_Should_Call_Gateway_And_Return_Response()
		{
			// Arrange
			var request = new TemporaryCodeUploadsSearchRequest
			{
				PageNumber = 1
			};

			var expected = new TemporaryCodeUploadSearchResponse
			{
				PageNumber = 1,
				PageSize = 10,
				TotalNumberOfRecords = 2,
				Data = new List<TemporaryCodeUploadResponse>
		{
			new()
			{
				Filename = "test.xlsx",
				NumberOfCodes = 100,
				FirstCode = "ABC001",
				LastCode = "ABC100",
				ImportedBy = "Tester",
				DateImported = DateTime.UtcNow
			}
		}
			};

			_mockGateway
				.Setup(x => x.GetTemporaryCodeUploads(request))
				.ReturnsAsync(expected);

			// Act
			var result = await _sut.Execute(request);

			// Assert
			result.Should().BeEquivalentTo(expected);
		}
	}
}