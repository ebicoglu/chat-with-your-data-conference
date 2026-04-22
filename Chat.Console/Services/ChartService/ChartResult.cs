namespace Chat.Console.Services.ChartService;

/// <summary>
/// Result object for chart generation
/// </summary>
public class ChartResult
{
    public bool Success { get; set; }
    public string VegaSpec { get; set; }
    public string ChartType { get; set; }
    public int DataRowCount { get; set; }
    public string RecommendedTitle { get; set; }
    public string ErrorMessage { get; set; }
}