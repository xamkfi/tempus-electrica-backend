# Tempus Electrica Backend

## Project Overview

**Tempus Electrica Backend** is a C# service that calculates electricity consumption prices based on various inputs such as spot prices, fixed prices, and historical consumption data. The project follows a layered architecture, separating concerns such as data transfer, business logic, and data access.

The project leverages extension methods for mapping between DTOs and domain entities, and uses interfaces to abstract service implementations for improved modularity and testability.

### Key Features:
- Calculation of electricity consumption prices based on user input or historical data.
- Support for both spot price and fixed price calculations.
- Background services for fetching and storing electricity price data from external sources.
- Structured mapping between DTOs (Data Transfer Objects) and domain entities.
- Use of custom validation for DTOs, with potential to integrate FluentValidation.

---

## Getting Started

### Prerequisites
- **.NET 8.0 SDK** or higher
- **SQL Server** (localdb or your own instance)
- **Visual Studio 2022** or another C# compatible IDE
- (Optional) **Azure Key Vault** for production secrets

### Clone the Repository
```bash
git clone https://github.com/your-repo/tempus-electrica-backend.git
cd tempus-electrica-backend
```

### Configuration

#### Local Development
- The default connection string uses `(localdb)\MSSQLLocalDB` and is set in `DatabaseMicroService/appsettings.json`:
  ```json
  "ConnectionStrings": {
    "ElectricityPriceDataContext": "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True;..."
  }
  ```
- You can override this by editing `appsettings.json` or using [User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets).
- For production, secrets (like the DB connection string) are expected from Azure Key Vault.

#### Other Settings
- `HealthChecks.ElectricityService.Url` should be set to your deployed API address if using health checks.
- `ServiceUrls.SpotPriceUrl` is the external API for spot prices.

### Install Dependencies
Restore all NuGet packages:
```bash
dotnet restore
```

### Build the Solution
```bash
dotnet build
```

### Database Setup
- Ensure your SQL Server is running and accessible.
- If using migrations, you may need to create and update the database:
  ```bash
  dotnet ef database update --project DatabaseMicroService
  ```

### Running the Service
From the root directory:
```bash
dotnet run --project DatabaseMicroService
```
- By default, the API will be available at `https://localhost:7122` and `http://localhost:5128` (see `launchSettings.json`).
- Swagger UI is enabled in development at `/swagger`.

---

## API Usage

### Endpoints

#### 1. Get Electricity Prices for a Period
- **GET** `/api/ElectricityPriceData/GetPricesForPeriod?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD`
- **Description:** Returns electricity prices for the specified date range.
- **Query Parameters:**
  - `startDate` (required): Start date (ISO format)
  - `endDate` (required): End date (ISO format)
- **Response:** `200 OK` with a list of price data.

#### 2. Upload Fingrid Consumption File
- **POST** `/api/ElectricityPriceData/UploadFinGridConsumptionFile?fixedPrice=VALUE[&marginal=VALUE]`
- **Description:** Upload a CSV file with consumption data and calculate total prices.
- **Form Data:**
  - `file`: CSV file (required)
- **Query Parameters:**
  - `fixedPrice` (required): Fixed price per kWh
  - `marginal` (optional): Marginal cost
- **Response:** `200 OK` with a summary object:
  ```json
  {
    "totalSpotPrice": 123.45,
    "totalFixedPrice": 120.00,
    "totalConsumption": 1000.0,
    "monthlyData": { "(5,2024)": { ... } },
    "weeklyData": { "(18,2024)": { ... } },
    "dailyData": { "2024-05-01": { ... } }
  }
  ```

#### 3. Calculate Price and Consumption
- **POST** `/api/ElectricityPriceData/CalculatePriceAndConsumption`
- **Description:** Calculates price and consumption based on user input.
- **Query Parameters:** All fields of `CombinedRequestDtoIn` (see below)
- **Example:**
  ```bash
  curl -X POST "https://localhost:7122/api/ElectricityPriceData/CalculatePriceAndConsumption?Year=2024&FixedPrice=0.12&HouseType=ApartmentHouse&SquareMeters=80&WorkShiftType=DayWorker&HeatingType=ElectricHeating&HasElectricCar=false&HasSauna=true&SaunaHeatingFrequency=2&HasFireplace=false&NumberOfResidents=2&HasSolarPanel=false&HasFloorHeating=false"
  ```
- **Required Fields:**
  - `Year` (int)
  - `FixedPrice` (decimal)
  - `HouseType` (ApartmentHouse, TerracedHouse, DetachedHouse, Cottage)
  - `SquareMeters` (decimal)
  - `WorkShiftType` (DayWorker, ShiftWorker, RemoteWorker)
  - `HeatingType` (ElectricHeating, DistrictHeating, GeothermalHeating, OilHeating)
  - `HasElectricCar` (bool)
  - `HasSauna` (bool)
  - `SaunaHeatingFrequency` (int, required if HasSauna)
  - `HasFireplace` (bool)
  - `FireplaceFrequency` (int, required if HasFireplace)
  - `NumberOfResidents` (int)
  - `HasSolarPanel` (bool)
  - `SolarPanel` (int, required if HasSolarPanel)
  - `HasFloorHeating` (bool)
  - `FloorSquareMeters` (decimal, required if HasFloorHeating)
- **Response:** `200 OK` with a summary object:
  ```json
  {
    "TotalFixedPriceCost": 120.00,
    "TotalSpotPriceCost": 123.45,
    "TotalDirectiveConsumption": 1000.0,
    "EstimatedMinConsumption": 900.0,
    "EstimatedMaxConsumption": 1100.0,
    "MinFixedPriceCost": 100.0,
    "MaxFixedPriceCost": 140.0,
    "MinSpotPriceCost": 105.0,
    "MaxSpotPriceCost": 145.0,
    "CalculationYears": "2024 - 2025",
    "CheaperOption": "Fixed",
    "CostDifference": 3.45,
    "AverageHourlySpotPrice": 0.13,
    "MonthlyData": [ ... ]
  }
  ```

---

## Testing

Unit tests are written using xUnit and Moq. To run all tests:
```bash
dotnet test
```
Tests are located in the `TestProject` directory.

---

## Contributing

Contributions are welcome! Please fork the repository and submit a pull request.

---

## License

This project is licensed under the MIT License. See the LICENSE file for details.
