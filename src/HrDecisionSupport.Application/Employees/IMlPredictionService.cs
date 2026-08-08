namespace HrDecisionSupport.Application.Employees;

public interface IMlPredictionService
{
    Task<int> PredictStayAsync(double shortestJobMonths, double longestJobMonths);
}