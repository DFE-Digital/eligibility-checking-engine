using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Gateways;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckYourEligibility.API.Tests.Gateways
{
	internal class TemporaryCodeUploadGatewayTests
	{
		private Mock<ILogger<TemporaryCodeUploadGateway>> _mockLogger = null!;
		private IEligibilityCheckContext _fakeInMemoryDb = null!;
		private TemporaryCodeUploadGateway _sut = null!;

		private User _user = null;

		[SetUp]
		public async Task Setup()
		{
			_mockLogger = new Mock<ILogger<TemporaryCodeUploadGateway>>();

			var options = TestDbOptions.Create(nameof(TemporaryCodeUploadGatewayTests));

			_user = new User
			{
				UserID = "test-user",
				UserName = "Test User",
				Email = "test@test.com",
				Reference = "TEST"
			};

			_fakeInMemoryDb = new EligibilityCheckContext(options);

			var context = (EligibilityCheckContext)_fakeInMemoryDb;

			await context.Database.EnsureDeletedAsync();
			await context.Database.EnsureCreatedAsync();

			_sut = new TemporaryCodeUploadGateway(
				_fakeInMemoryDb,
				_mockLogger.Object);
		}

		[TearDown]
		public async Task TearDown()
		{
			var context = (EligibilityCheckContext)_fakeInMemoryDb;
			await context.Database.EnsureDeletedAsync();
		}

		[Test]
		public async Task GetTemporaryCodeUploads_Should_Return_Uploads()
		{
			// Arrange
			_fakeInMemoryDb.Users.Add(_user);

			_fakeInMemoryDb.TemporaryCodeUploads.Add(
				new TemporaryCodeUpload
				{
					TemporaryCodeUploadId = Guid.NewGuid(),
					Filename = "Test.xlsx",
					NumberOfCodes = 10,
					FirstCode = "CODE001",
					LastCode = "CODE010",
					DateImported = DateTime.UtcNow,
					ImportedBy = _user.UserID,
					ImportedByUser = _user,
					IsDeleted = false
				});

			await _fakeInMemoryDb.SaveChangesAsync();

			var request = new TemporaryCodeUploadsSearchRequest
			{
				PageNumber = 1,
				PageSize = 10
			};

			// Act
			var result = await _sut.GetTemporaryCodeUploads(request);

			// Assert
			result.Should().NotBeNull();
			result.TotalNumberOfRecords.Should().Be(1);
			result.Data.Should().ContainSingle();
		}

		[Test]
		public async Task GetTemporaryCodeUploads_Should_Exclude_Deleted_Records()
		{
			_fakeInMemoryDb.Users.Add(_user);

			_fakeInMemoryDb.TemporaryCodeUploads.AddRange(
				new TemporaryCodeUpload
				{
					TemporaryCodeUploadId = Guid.NewGuid(),
					Filename = "Active.xlsx",
					NumberOfCodes = 10,
					FirstCode = "CODE001",
					LastCode = "CODE010",
					DateImported = DateTime.UtcNow,
					ImportedBy = _user.UserID,
					ImportedByUser = _user,
					IsDeleted = false
				},
				new TemporaryCodeUpload
				{
					TemporaryCodeUploadId = Guid.NewGuid(),
					Filename = "Deleted.xlsx",
					NumberOfCodes = 10,
					FirstCode = "CODE011",
					LastCode = "CODE020",
					DateImported = DateTime.UtcNow,
					ImportedBy = _user.UserID,
					ImportedByUser = _user,
					IsDeleted = true
				});

			await _fakeInMemoryDb.SaveChangesAsync();

			var request = new TemporaryCodeUploadsSearchRequest
			{
				PageNumber = 1,
				PageSize = 10
			};

			// Act
			var result = await _sut.GetTemporaryCodeUploads(request);

			// Assert
			result.Should().NotBeNull();
			result.TotalNumberOfRecords.Should().Be(1);
			result.Data.Should().ContainSingle();

			var upload = result.Data.First();

			upload.Filename.Should().Be("Active.xlsx");
		}

		[Test]
		public async Task GetTemporaryCodeUploads_Should_Exclude_Records_Older_Than_30_Days()
		{
			// Arrange
			_fakeInMemoryDb.TemporaryCodeUploads.AddRange(
				new TemporaryCodeUpload
				{
					TemporaryCodeUploadId = Guid.NewGuid(),
					Filename = "Recent.xlsx",
					NumberOfCodes = 10,
					FirstCode = "CODE001",
					LastCode = "CODE010",
					DateImported = DateTime.UtcNow.AddDays(-5),
					ImportedBy = _user.UserID,
					ImportedByUser = _user,
					IsDeleted = false
				},
				new TemporaryCodeUpload
				{
					TemporaryCodeUploadId = Guid.NewGuid(),
					Filename = "Old.xlsx",
					NumberOfCodes = 10,
					FirstCode = "CODE011",
					LastCode = "CODE020",
					DateImported = DateTime.UtcNow.AddDays(-40),
					ImportedBy = _user.UserID,
					ImportedByUser = _user,
					IsDeleted = false
				});

			await _fakeInMemoryDb.SaveChangesAsync();

			var request = new TemporaryCodeUploadsSearchRequest
			{
				PageNumber = 1,
				PageSize = 10
			};

			// Act
			var result = await _sut.GetTemporaryCodeUploads(request);

			// Assert
			result.TotalNumberOfRecords.Should().Be(1);
			result.Data.Should().ContainSingle();
			result.Data.First().Filename.Should().Be("Recent.xlsx");
		}

		[Test]
		public async Task GetTemporaryCodeUploads_Should_Default_To_Page_1_When_PageNumber_Is_Less_Than_1()
		{
			// Arrange
			_fakeInMemoryDb.TemporaryCodeUploads.Add(
				new TemporaryCodeUpload
				{
					TemporaryCodeUploadId = Guid.NewGuid(),
					Filename = "Test.xlsx",
					NumberOfCodes = 10,
					FirstCode = "CODE001",
					LastCode = "CODE010",
					DateImported = DateTime.UtcNow,
					ImportedBy = _user.UserID,
					ImportedByUser = _user,
					IsDeleted = false
				});

			await _fakeInMemoryDb.SaveChangesAsync();

			var request = new TemporaryCodeUploadsSearchRequest
			{
				PageNumber = 0,
				PageSize = 10
			};

			// Act
			var result = await _sut.GetTemporaryCodeUploads(request);

			// Assert
			result.PageNumber.Should().Be(1);
		}

		[Test]
		public async Task GetTemporaryCodeUploads_Should_Return_Last_Page_When_PageNumber_Exceeds_Max_Page()
		{
			// Arrange
			for (int i = 1; i <= 11; i++)
			{
				_fakeInMemoryDb.TemporaryCodeUploads.Add(
					new TemporaryCodeUpload
					{
						TemporaryCodeUploadId = Guid.NewGuid(),
						Filename = $"File{i}.xlsx",
						NumberOfCodes = 10,
						FirstCode = $"CODE{i:D3}",
						LastCode = $"CODE{i + 9:D3}",
						DateImported = DateTime.UtcNow.AddMinutes(-i),
						ImportedBy = _user.UserID,
						ImportedByUser = _user,
						IsDeleted = false
					});
			}

			await _fakeInMemoryDb.SaveChangesAsync();

			var request = new TemporaryCodeUploadsSearchRequest
			{
				PageNumber = 99,
				PageSize = 10
			};

			// Act
			var result = await _sut.GetTemporaryCodeUploads(request);

			// Assert
			result.PageNumber.Should().Be(2);
			result.TotalNumberOfRecords.Should().Be(11);
		}

		[Test]
		public async Task GetTemporaryCodeUploads_Should_Return_Correct_Page_Of_Results()
		{
			// Arrange
			for (int i = 1; i <= 15; i++)
			{
				_fakeInMemoryDb.TemporaryCodeUploads.Add(
					new TemporaryCodeUpload
					{
						TemporaryCodeUploadId = Guid.NewGuid(),
						Filename = $"File{i}.xlsx",
						NumberOfCodes = 10,
						FirstCode = $"CODE{i:D3}",
						LastCode = $"CODE{i + 9:D3}",
						DateImported = DateTime.UtcNow.AddMinutes(-i),
						ImportedBy = _user.UserID,
						ImportedByUser = _user,
						IsDeleted = false
					});
			}

			await _fakeInMemoryDb.SaveChangesAsync();

			var request = new TemporaryCodeUploadsSearchRequest
			{
				PageNumber = 2,
				PageSize = 10
			};

			// Act
			var result = await _sut.GetTemporaryCodeUploads(request);

			// Assert
			result.PageNumber.Should().Be(2);
			result.TotalNumberOfRecords.Should().Be(15);
			result.Data.Should().HaveCount(5);
		}
	}
}