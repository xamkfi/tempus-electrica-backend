using System.Text.Json.Serialization;

namespace Domain.Entities
{
    public class ElectricityPriceData : BaseEntity
    {
        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }

        [JsonPropertyName("date")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        public ElectricityPriceData ToEntity()
        {
            return new ElectricityPriceData
            {
                Price = this.Price,
                StartDate = this.StartDate,
                EndDate = this.EndDate
            };
        }
    }

}
