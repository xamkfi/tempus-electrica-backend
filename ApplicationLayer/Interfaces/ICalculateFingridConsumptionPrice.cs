using ApplicationLayer.Dto.Consumption;

public interface ICalculateFingridConsumptionPrice
{
    Task<ConsumptionPriceCalculationResult> CalculateTotalConsumptionPricesAsync(string csvFilePath, decimal? fixedPrice, decimal? marginal);
}
