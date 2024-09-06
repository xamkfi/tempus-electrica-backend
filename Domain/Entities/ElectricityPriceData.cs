namespace Domain.Entities
{
    public class ElectricityPriceData : BaseEntity
    {
        public DateTime EndDate { get; set; }
        public DateTime StartDate { get; set; }
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
