using Domain.Entities;
using System.Text.Json.Serialization;
namespace ApplicationLayer.Dto
{
    public class ElectricityPricesResponse
    {
        [JsonPropertyName("prices")]
        public List<ElectricityPriceData> Prices { get; set; }

        public DateTime EndDate { get; set; }
    }
}