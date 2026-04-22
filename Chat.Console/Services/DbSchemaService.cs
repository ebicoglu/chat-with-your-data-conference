using System.Data;
using System.Data.SQLite;
using System.Text;
using Chat.Console.Utils;

namespace Chat.Console.Services;

public class DbSchemaService
{
    public static readonly string SchemaFilePath = Path.GetFullPath(Path.Combine("Output", "db-schema.txt"));

    public static async Task<string> GetSchema(string connectionString)
    {
        //Add a very simple cache!
        //Schema doesn't change frequently! If the schema is already created just return... 
        var fromCache = Helpers.ReadText(SchemaFilePath);
        if (fromCache != null)
        {
            return fromCache;
        }

        // Otherwise, fetch the schema from the database
        var dbSchema = new StringBuilder();
        await using (var connection = new SQLiteConnection(connectionString))
        {
            await connection.OpenAsync();
            var dataTable = await connection.GetSchemaAsync("Tables");
            foreach (DataRow row in dataTable.Rows)
            {
                dbSchema.AppendLine($"{row["TABLE_DEFINITION"]}\n");
            }
        }

        //Save the schema to a file for future use
        Helpers.SaveText(dbSchema.ToString(), SchemaFilePath);
        return dbSchema.ToString();
    }
}
