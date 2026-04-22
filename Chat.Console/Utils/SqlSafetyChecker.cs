using System.Text.RegularExpressions;

namespace Chat.Console.Utils;

// Provides centralized safety checks for AI-generated SQL before execution.
public static class SqlSafetyChecker
{
    public static bool IsSafe(string sql)
    {
        // Reject null, empty, or whitespace-only queries.
        if (string.IsNullOrWhiteSpace(sql))
        {
            return false;
        }

        var trimmedSql = sql.Trim().TrimEnd(';');

        //This regex pattern checks if the SQL query starts with SELECT or WITH (read-only query)
        if (!Regex.IsMatch(trimmedSql, @"^(SELECT|WITH)\b", RegexOptions.IgnoreCase))
        {
            return false;
        }

        // Prevent multi-statement execution and obvious SQL injection patterns.
        if (trimmedSql.Contains(';') || trimmedSql.Contains("--") || trimmedSql.Contains("/*"))
        {
            return false;
        }

        // Reject queries that include disallowed SQL command keywords.
        // Write-oriented and schema-changing commands are not allowed.
        string[] forbiddenKeywords =
        [
            "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE", "TRUNCATE",
            "REPLACE", "MERGE", "ATTACH", "DETACH", "PRAGMA", "VACUUM",
            "REINDEX", "ANALYZE", "BEGIN", "COMMIT", "ROLLBACK"
        ];
        return forbiddenKeywords.All(keyword => !Regex.IsMatch(trimmedSql, $@"\b{keyword}\b", RegexOptions.IgnoreCase));
    }
}
