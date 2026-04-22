using System.Data;

namespace Chat.Console.Services.ChartService;

/// <summary>
/// Service that integrates SQL query execution with chart generation
/// Using this open-source charting library -> https://github.com/vega/vega
/// </summary>
public static class ChartGenerationService
{
    private static readonly VegaChartGenerator ChartGenerator = new();
    private static readonly ChartRecommendationService RecommendationService = new();

    /// <summary>
    /// Complete pipeline: Execute SQL -> Generate Chart
    /// </summary>
    public static async Task<ChartResult> GenerateChartFromDataTable(
        DataTable dataTable,
        string? userQuery = null,
        string? chartType = null,
        string? title = null)
    {
        try
        {
            // Don't generate chart if no data
            if (dataTable.Rows.Count == 0)
            {
                return new ChartResult
                {
                    Success = false,
                    ErrorMessage = "No data returned from SQL query"
                };
            }

            // Recommend chart type if not specified
            if (string.IsNullOrEmpty(chartType))
            {
                chartType = await RecommendationService.RecommendChartType(dataTable, userQuery);
            }

            // Generate chart
            var vegaSpec = ChartGenerator.GenerateChart(dataTable, chartType, title);

            return new ChartResult
            {
                Success = true,
                VegaSpec = vegaSpec,
                ChartType = chartType,
                DataRowCount = dataTable.Rows.Count,
                RecommendedTitle = title 
            };
        }
        catch (Exception ex)
        {
            return new ChartResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}