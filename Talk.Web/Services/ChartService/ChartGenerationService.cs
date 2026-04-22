using System.Data;

namespace Talk.Web.Services.ChartService;

/// <summary>
/// Orchestrates chart-type recommendation and Vega spec generation.
/// </summary>
public class ChartGenerationService
{
    private readonly VegaChartGenerator _chartGenerator;
    private readonly ChartRecommendationService _recommendationService;

    public ChartGenerationService(VegaChartGenerator chartGenerator, ChartRecommendationService recommendationService)
    {
        _chartGenerator = chartGenerator;
        _recommendationService = recommendationService;
    }

    public async Task<ChartResult> GenerateChartFromDataTable(
        DataTable dataTable,
        string? userQuery = null,
        string? chartType = null,
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (dataTable.Rows.Count == 0)
            {
                return new ChartResult
                {
                    Success = false,
                    ErrorMessage = "No data to visualize."
                };
            }

            if (string.IsNullOrEmpty(chartType))
            {
                chartType = await _recommendationService.RecommendChartType(dataTable, userQuery, cancellationToken);
            }

            var vegaSpec = _chartGenerator.GenerateChart(dataTable, chartType, title);

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
