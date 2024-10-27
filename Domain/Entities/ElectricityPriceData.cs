using System.Text.Json.Serialization;

namespace Domain.Entities
{
    public class ElectricityPriceData : BaseEntity
    {
        
        public DateTime EndDate { get; set; }

        [JsonPropertyName("date")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("value")]
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
