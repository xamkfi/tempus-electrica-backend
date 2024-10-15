using ApplicationLayer.Dto;
using Domain.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Interfaces
{
    public interface IConsumptionDataProcessor
    {
        ProcessedCsvDataResult ProcessConsumptionData(
            ConcurrentDictionary<DateTime, decimal> hourlyConsumption,
            List<ElectricityPriceData> electricityPrices,
            decimal? fixedPrice);
    }
}
