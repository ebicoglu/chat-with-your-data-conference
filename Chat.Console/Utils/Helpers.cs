using System.Text.RegularExpressions;

namespace Chat.Console.Utils;

public class Helpers
{
    public static void SaveBytes(byte[] excel, string filePath)
    {
        CreatePathIfNotExists(filePath);
        File.WriteAllBytes(filePath, excel);
    }

    public static void SaveText(string text, string filePath)
    {
        CreatePathIfNotExists(filePath);
        File.WriteAllText(filePath, text);
    }

    public static string? ReadText(string filePath)
    {
        return !File.Exists(filePath) ? null : File.ReadAllText(filePath);
    }

    //Some mini LLMs add code-blocks and extra text that's why this method extracts the actual SQL
    public static string ExtractSqlQuery(string rawAiResponse)
    {
        var match = Regex.Match(rawAiResponse, @"```(?:sql)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : rawAiResponse.Trim();
    }

    private static void CreatePathIfNotExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}