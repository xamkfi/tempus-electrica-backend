using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace ApplicationLayer.Dto
{
    public class CombinedRequestDtoIn : IValidatableObject
    {
        [Required(ErrorMessage = "Missing year.")]
        public int Year { get; set; }

        [Required, Range(0.01, double.MaxValue, ErrorMessage = "Fixed price must be greater than zero.")]
        public decimal FixedPrice { get; set; }

        public void ValidateModel()
        {
            if (Year == 0)
            {
                throw new ValidationException("Year is required.");
            }
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var validationResults = new List<ValidationResult>();

            if (NumberOfResidents == 0)
            {
                validationResults.Add(new ValidationResult("You must select at least one resident.", [nameof(NumberOfResidents)]));
            }

            if (HasSauna || HasFireplace)
            {
                if (HasSauna && !SaunaHeatingFrequency.HasValue)
                {
                    validationResults.Add(new ValidationResult("Sauna heating frequency is required when sauna is selected.", [nameof(SaunaHeatingFrequency)]));
                }

                if (HasFireplace && !FireplaceFrequency.HasValue)
                {
                    validationResults.Add(new ValidationResult("Fireplace heating frequency is required when fireplace is selected.", [nameof(FireplaceFrequency)]));
                }
            }

            if (HasElectricCar)
            {
                if (!NumberOfCars.HasValue || NumberOfCars == 0)
                {
                    validationResults.Add(new ValidationResult("Number of cars is required when electric car is selected and must be greater than zero.", [nameof(NumberOfCars)]));
                }

                if (!ElectricCarkWhUsagePerYear.HasValue || ElectricCarkWhUsagePerYear <= 0)
                {
                    validationResults.Add(new ValidationResult("Electric car kWh usage per year is required and must be greater than zero when electric car is selected.", [nameof(ElectricCarkWhUsagePerYear)]));
                }
            }

            if ((HouseType == HouseType.ApartmentHouse || HouseType == HouseType.TerracedHouse) && HasFloorHeating)
            {
                if (!FloorSquareMeters.HasValue || FloorSquareMeters <= 0)
                {
                    validationResults.Add(new ValidationResult("Floor square meters is required and must be greater than zero when floor heating is selected in an apartment or terraced house.", [nameof(FloorSquareMeters)]));
                }
            }

            if ((HouseType == HouseType.DetachedHouse || HouseType == HouseType.Cottage) && HasSolarPanel)
            {
                if (!SolarPanel.HasValue || SolarPanel <= 0)
                {
                    validationResults.Add(new ValidationResult("Solar panel count is required and must be greater than zero when solar panel is selected in an detached house or cottage.", [nameof(SolarPanel)]));
                }
            }

            return validationResults;
        }

        [Required]
        public HouseType HouseType { get; set; }

        [Required]
        public decimal SquareMeters { get; set; }

        [Required]
        public WorkShiftType WorkShiftType { get; set; }

        [Required]
        public HeatingType HeatingType { get; set; }

        [Required]
        public bool HasElectricCar { get; set; }
        [Required]
        public bool HasSauna { get; set; }
        public int? SaunaHeatingFrequency { get; set; }
        [Required]
        public bool HasFireplace { get; set; }
        public int? FireplaceFrequency { get; set; }

        [Range(0, 9)]
        public int? NumberOfCars { get; set; }

        [Required, Range(0, 9)]
        public int NumberOfResidents { get; set; }
        public int? ElectricCarkWhUsagePerYear { get; set; }
        [Required]
        public bool HasSolarPanel { get; set; }
        public int? SolarPanel { get; set; }
        [Required]
        public bool HasFloorHeating { get; set; }
        public decimal? FloorSquareMeters { get; set; }
    }

    public enum HouseType
    {
        [EnumMember(Value = "ApartmentHouse")]
        ApartmentHouse,
        [EnumMember(Value = "TerracedHouse")]
        TerracedHouse,
        [EnumMember(Value = "DetachedHouse")]
        DetachedHouse,
        [EnumMember(Value = "Cottage")]
        Cottage
    }

    public enum WorkShiftType
    {
        [EnumMember(Value = "DayWorker")]
        DayWorker,
        [EnumMember(Value = "ShiftWorker")]
        ShiftWorker,
        [EnumMember(Value = "RemoteWorker")]
        RemoteWorker
    }

    public enum HeatingType
    {
        [EnumMember(Value = "ElectricHeating")]
        ElectricHeating,
        [EnumMember(Value = "DistrictHeating")]
        DistrictHeating,
        [EnumMember(Value = "GeothermalHeating")]
        GeothermalHeating,
        [EnumMember(Value = "OilHeating")]
        OilHeating
    }

    public enum ElectricityPriceType
    {
        [EnumMember(Value = "Day")]
        Day = 0,
        [EnumMember(Value = "Evening")]
        Evening = 1,
        [EnumMember(Value = "Night")]
        Night = 2
    }
}