using CheckYourEligibility.Core.Helpers;

namespace CheckYourEligibility.API.Tests.Helpers
{
    public class EligibilityCheckHelperTests
    {
        [TestCase("2026-07-10", "2027-07-31")]
        [TestCase("2026-12-01", "2027-07-31")]
        [TestCase("2027-02-03", "2027-07-31")]
        [TestCase("2027-05-05", "2027-07-31")]
        [TestCase("2027-07-01", "2028-07-31")]
        public void GetEligibilityEndDateFSM_ReturnsExpectedEndDate(string checkDateString, string expectedEndDateString)
        {
            // Arrange
            var checkDate = DateTime.Parse(checkDateString);
            var expectedEndDate = DateTime.Parse(expectedEndDateString);

            // Act
            var result = EligibilityCheckHelper.GetEligibilityEndDateFSM(checkDate);

            // Assert
            Assert.That(result, Is.EqualTo(expectedEndDate));
        }
    }
}
