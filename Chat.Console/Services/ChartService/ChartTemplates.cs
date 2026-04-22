using Chat.Console.Utils;

namespace Chat.Console.Services.ChartService;

/// <summary>
/// Advanced chart configuration for specific use cases
/// </summary>
public static class ChartTemplates
{
    private static readonly string DefaultFilePath = Path.GetFullPath(Path.Combine("Output", "chart.html"));

    public static string GenerateChartHtml(string vegaSpec, string title)
    {
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Chart Report</title>
    <script src='https://cdn.jsdelivr.net/npm/vega@5'></script>
    <script src='https://cdn.jsdelivr.net/npm/vega-lite@5'></script>
    <script src='https://cdn.jsdelivr.net/npm/vega-embed@6'></script>
    <style>
        body {{ font-family: Arial, sans-serif;  }}
    </style>
</head>
<body>
    <h1>{title}</h1>
    <div id='chart'></div>
    <script>
        vegaEmbed('#chart', {vegaSpec}, {{
            actions: {{ 
                export: true, 
                source: false, 
                compiled: false, 
                editor: false 
            }}
        }});
    </script>
</body>
</html>";

        Helpers.SaveText(html, DefaultFilePath);
        return DefaultFilePath;
    }
}