using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ApplicationLayer.Dto
{
    public class ElectricityPricesResponse
    {
        [JsonPropertyName("prices")]
        public List<ElectricityPriceData> Prices { get; set; }
    }
}
