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

## Table of Contents
1. [Project Structure](#project-structure)
2. [Installation](#installation)
3. [Usage](#usage)
4. [Architecture](#architecture)
5. [Testing](#testing)
6. [Future Improvements](#future-improvements)

---

## Project Structure

The project follows a layered architecture, ensuring separation of concerns and clean organization. Below is an overview of key directories:

```bash
.
├── ApplicationLayer
│   ├── Dto
│   ├── Extensions
│   ├── Interfaces
├── Domain
│   ├── Entities
├── Infrastructure
│   ├── Repositories
├── TestProject
│   ├── Application
└── DatabaseMicroService.sln
```

---

## Installation

### Prerequisites
- .NET SDK 8.0 or higher
- Visual Studio 2022 or another C# compatible IDE
- NuGet packages: Ensure required NuGet packages (e.g., FluentValidation, Moq) are installed.

### Setup Instructions

1. Clone the repository:
   ```bash
   git clone https://github.com/your-repo/tempus-electrica-backend.git
   ```

2. Navigate to the project directory:
   ```bash
   cd tempus-electrica-backend
   ```

3. Restore the dependencies:
   ```bash
   dotnet restore
   ```

4. Build the solution:
   ```bash
   dotnet build
   ```

5. Run the project:
   ```bash
   dotnet run
   ```

---

## Usage

### Key Services:
- **ElectricityService**:
   - Adds electricity prices from DTOs to the database using repository methods.
   - Maps PriceInfo DTOs to ElectricityPriceData domain entities via extension methods.

- **ICalculateFingridConsumptionPrice**:
   - Provides a service for calculating total electricity consumption prices.
   - Returns a structured result using the `ConsumptionPriceResultDto`.

### Example Code Usage:
```csharp
var electricityPriceDataDtoIn = new ElectricityPriceDataDtoIn
{
    Prices = new List<PriceInfo>
    {
        new PriceInfo { StartDate = DateTime.Now.AddHours(-1), EndDate = DateTime.Now, Price = 10m }
    }
};

// Use ElectricityService to add electricity prices
await _electricityService.AddElectricityPricesAsync(electricityPriceDataDtoIn);
```

---

## Architecture

The project uses a layered architecture:

- **Application Layer**: Contains business logic and DTOs for data transfer between layers.
- **Domain Layer**: Contains core entities, such as `ElectricityPriceData`.
- **Infrastructure Layer**: Handles persistence through repositories, abstracted by interfaces.

The mapping between DTOs and domain entities is handled via extension methods (`ElectricityPriceDataExtensions`), ensuring that the conversion logic remains clean and reusable.

---

## Testing

### Running Tests:
Unit tests are written using the xUnit framework and Moq for mocking dependencies.

To run the tests, execute the following command:

```bash
dotnet test
```

Tests can be found in the `TestProject` directory.

---

## Future Improvements

- **FluentValidation Integration**: Simplify DTO validation by integrating FluentValidation to handle validation rules outside the DTO classes.
- **Automated Background Jobs**: Implement recurring jobs for fetching and storing electricity prices using tools like Hangfire or Quartz.NET.
- **Performance Improvements**: Optimize data retrieval by implementing pagination and caching in repository methods.

---

## Contributing

Feel free to fork the repository and submit pull requests. We welcome all contributions that improve the project!

---

## License

This project is licensed under the MIT License - see the LICENSE file for details.
