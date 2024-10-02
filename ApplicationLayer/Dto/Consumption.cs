using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Dto
{
    internal class Consumption
    {
        public decimal Total { get; set; }
        public decimal HousingConsumption { get; set; }
        public decimal WorkShiftConsumption { get; set; }
        public decimal HeatingConsumption { get; set; }
        public decimal SaunaConsumption { get; set; }
        public decimal FireplaceSavings { get; set; }
        public decimal ElectricCarConsumption { get; set; }
        public decimal ResidentConsumption { get; set; }
        public decimal FloorHeatingConsumption { get; set; }
        public decimal SolarPanelSavings { get; set; }
        public WorkShiftType WorkShiftType { get; set; }
        public decimal MinConsumption { get; set; }
        public decimal MaxConsumption { get; set; }
    }
}
