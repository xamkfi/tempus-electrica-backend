using ApplicationLayer.Services;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace ApplicationLayer.Tests
{
    public class SaveHistoryDataServiceTests
    {
        private readonly Mock<IElectricityRepository> _mockElectricityRepository;
        private readonly Mock<ILogger<SaveHistoryDataService>> _mockLogger;
        private readonly SaveHistoryDataService _saveHistoryDataService;

        public SaveHistoryDataServiceTests()
        {
            _mockElectricityRepository = new Mock<IElectricityRepository>();
            _mockLogger = new Mock<ILogger<SaveHistoryDataService>>();
            _saveHistoryDataService = new SaveHistoryDataService(_mockElectricityRepository.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task ProcessCsvFileAsync_ShouldReturnTrue_WhenFileIsProcessedSuccessfully()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            var content = "Timestamp;Price\n2024-05-01T00:00:00;100.5\n2024-05-01T01:00:00;150.75";
            var fileName = "test.csv";
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);
            writer.Write(content);
            writer.Flush();
            ms.Position = 0;
            mockFile.Setup(f => f.OpenReadStream()).Returns(ms);
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.Length).Returns(ms.Length);

            _mockElectricityRepository
                .Setup(repo => repo.GetDuplicatesAsync(It.IsAny<List<DateTime>>(), It.IsAny<List<DateTime>>()))
                .ReturnsAsync(new List<ElectricityPriceData>());

            _mockElectricityRepository
                .Setup(repo => repo.AddBatchElectricityPricesAsync(It.IsAny<IEnumerable<ElectricityPriceData>>()))
                .ReturnsAsync(true);

            // Act
            var result = await _saveHistoryDataService.ProcessCsvFileAsync(mockFile.Object);

            // Assert
            Assert.True(result);
            _mockElectricityRepository.Verify(repo => repo.AddBatchElectricityPricesAsync(It.IsAny<IEnumerable<ElectricityPriceData>>()), Times.Once);
        }

        [Fact]
        public async Task ProcessCsvFileAsync_ShouldReturnFalse_WhenExceptionIsThrown()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            var content = "Timestamp;Price\n2024-05-01T00:00:00;100.5\n2024-05-01T01:00:00;150.75";
            var fileName = "test.csv";
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);
            writer.Write(content);
            writer.Flush();
            ms.Position = 0;
            mockFile.Setup(f => f.OpenReadStream()).Returns(ms);
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.Length).Returns(ms.Length);

            _mockElectricityRepository
                .Setup(repo => repo.GetDuplicatesAsync(It.IsAny<List<DateTime>>(), It.IsAny<List<DateTime>>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _saveHistoryDataService.ProcessCsvFileAsync(mockFile.Object);

            // Assert
            Assert.False(result);
            _mockLogger.Verify(
                x => x.Log(
                    It.Is<LogLevel>(l => l == LogLevel.Error),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error processing electricity price data")),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessCsvFileAsync_ShouldNotProcessRecords_WhenFileIsEmpty()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            var content = "Timestamp;Price";
            var fileName = "test.csv";
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);
            writer.Write(content);
            writer.Flush();
            ms.Position = 0;
            mockFile.Setup(f => f.OpenReadStream()).Returns(ms);
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.Length).Returns(ms.Length);

            // Act
            var result = await _saveHistoryDataService.ProcessCsvFileAsync(mockFile.Object);

            // Assert
            Assert.True(result);
            _mockElectricityRepository.Verify(repo => repo.AddBatchElectricityPricesAsync(It.IsAny<IEnumerable<ElectricityPriceData>>()), Times.Never);
        }
    }
}
