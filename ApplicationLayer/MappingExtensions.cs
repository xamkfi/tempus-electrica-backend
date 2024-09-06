using ApplicationLayer.Dto;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer
{
    public static class MappingExtensions
    {
        public static ElectricityPriceData ToEntity(this PriceInfo priceInfo)
        {
            return new ElectricityPriceData
            {
                StartDate = priceInfo.StartDate,
                EndDate = priceInfo.EndDate,
                Price = priceInfo.Price
            };
        }
    }
}
