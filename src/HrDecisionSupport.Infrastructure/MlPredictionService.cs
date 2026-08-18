using HrDecisionSupport.Application.Employees;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HrDecisionSupport.Infrastructure;

public class MlPredictionService : IMlPredictionService
{
    private readonly HttpClient _httpClient;

    public MlPredictionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("http://127.0.0.1:8000/"); // Python API Adresi
    }

    public async Task<int> PredictStayAsync(
        double shortestJobMonths,
        double longestJobMonths,
        CancellationToken cancellationToken = default)
    {
        var payload = new { shortest_job_months = shortestJobMonths, longest_job_months = longestJobMonths };
        var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("predict", content, cancellationToken);

        if (!response.IsSuccessStatusCode) return -1; // Hata durumu

        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
        using var jsonDoc = JsonDocument.Parse(responseString);
        return jsonDoc.RootElement.GetProperty("label").GetInt32();
    }
}
