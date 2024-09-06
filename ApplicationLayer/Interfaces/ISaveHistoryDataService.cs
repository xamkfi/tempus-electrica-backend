using Microsoft.AspNetCore.Http;


namespace ApplicationLayer.Interfaces
{
    public interface ISaveHistoryDataService
    {
        Task<bool> ProcessCsvFileAsync(IFormFile file);
    }
}
