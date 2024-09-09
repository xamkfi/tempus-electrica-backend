namespace ApplicationLayer.Dto
{
    public class MonthlyConsumptionData
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Consumption { get; set; }
        public decimal SpotPrice { get; set; }
        public decimal FixedPrice { get; set; }
    }

    public class WeeklyConsumptionData
    {
        public int Year { get; set; }
        public int Week { get; set; }
        public decimal Consumption { get; set; }
        public decimal SpotPrice { get; set; }
        public decimal FixedPrice { get; set; }
    }

    public class DailyConsumptionData
    {

        public string? Day { get; set; }
        public decimal Consumption { get; set; }
        public decimal SpotPrice { get; set; }
        public decimal FixedPrice { get; set; }



    }
}
