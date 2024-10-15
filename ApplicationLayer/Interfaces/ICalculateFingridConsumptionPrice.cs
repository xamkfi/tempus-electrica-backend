using ApplicationLayer.Dto;

public interface ICalculateFingridConsumptionPrice
{
    Task<ConsumptionPriceCalculationResult> CalculateTotalConsumptionPricesAsync(string csvFilePath, decimal? fixedPrice);
}
