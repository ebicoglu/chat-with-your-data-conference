namespace Chat.Console.Utils;

public static class ConsoleHelper
{
    public static void WriteInfo(string text)
    {
        System.Console.ForegroundColor = ConsoleColor.Yellow;
        System.Console.WriteLine( Environment.NewLine + text);
        System.Console.ForegroundColor = ConsoleColor.White;
    }

    public static void WriteError(string text)
    {
        System.Console.ForegroundColor = ConsoleColor.Red;
        System.Console.WriteLine(Environment.NewLine + text);
        System.Console.ForegroundColor = ConsoleColor.White;
    }

    public static void Print(System.Data.DataTable dt)
    {
        if (dt.Rows.Count == 1 && dt.Columns.Count == 1)
        {
            System.Console.WriteLine(dt.Rows[0][0]);
        }
        else
        {
            // Calculate maximum width for each column
            int[] columnWidths = new int[dt.Columns.Count];

            // Get maximum width for column headers
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                columnWidths[i] = dt.Columns[i].ColumnName.Length;
            }

            // Get maximum width for data in each column
            foreach (System.Data.DataRow row in dt.Rows)
            {
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    string value = row[i]?.ToString() ?? "";
                    columnWidths[i] = Math.Max(columnWidths[i], value.Length);
                }
            }

            // Print headers
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                System.Console.Write(dt.Columns[i].ColumnName.PadRight(columnWidths[i] + 2));
            }
            System.Console.WriteLine();

            // Print separator line
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                System.Console.Write(new string('-', columnWidths[i] + 2));
            }
            System.Console.WriteLine();

            // Print data rows
            foreach (System.Data.DataRow row in dt.Rows)
            {
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    string value = row[i]?.ToString() ?? "";
                    System.Console.Write(value.PadRight(columnWidths[i] + 2));
                }
                System.Console.WriteLine();
            }
        }
    }
}