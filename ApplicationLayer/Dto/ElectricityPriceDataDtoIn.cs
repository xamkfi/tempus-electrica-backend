using CsvHelper.Configuration.Attributes;


namespace ApplicationLayer.Dto
{
    public record  ElectricityPriceDataDtoIn
    {
        public required List<PriceInfo> Prices { get; set; }
    }

    public record PriceInfo
    {
        public decimal Price { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class ElectricityPriceCsvRecord
    {
        [Index(0)]
        public string? Timestamp{ get; set; }

        [Index(1)]
        public decimal Price { get; set; }
    }
    public record GetPricesForPeriodDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
