using ApplicationLayer.Dto;
using ApplicationLayer.Interfaces;
using DatabaseMicroService.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;


namespace TestProject.Presentation
{
    public class ElectricityPriceDataControllerTests
    {
        private readonly Mock<ILogger<ElectricityPriceDataController>> _loggerMock;
        private readonly Mock<ISaveHistoryDataService> _saveHistoryDataServiceMock;
        private readonly Mock<IDateRangeDataService> _dateRangeDataServiceMock;
        private readonly ElectricityPriceDataController _controller;
        private readonly DefaultHttpContext _httpContext;
        

        public ElectricityPriceDataControllerTests()
        {
            _loggerMock = new Mock<ILogger<ElectricityPriceDataController>>();
            _saveHistoryDataServiceMock = new Mock<ISaveHistoryDataService>();
            _dateRangeDataServiceMock = new Mock<IDateRangeDataService>();
            _controller = new ElectricityPriceDataController(
                _loggerMock.Object,
                _saveHistoryDataServiceMock.Object,
                _dateRangeDataServiceMock.Object
            );

            _httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = _httpContext
            };
        }


        [Fact]
        public async Task GetPricesForPeriod_ExceptionThrown_ReturnsInternalServerError()
        {
            //Arrange
            var request = new GetPricesForPeriodDto { StartDate = DateTime.Now.AddDays(-1), EndDate = DateTime.Now };
            _dateRangeDataServiceMock.Setup(service => service.GetPricesForPeriodAsync(request.StartDate, request.EndDate))
                .ThrowsAsync(new Exception());

            //Act
            var result = await _controller.GetPricesForPeriod(request);

            //Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
            Assert.Equal("Error getting electricity prices for the period.", statusCodeResult.Value);
        }
    }
}
