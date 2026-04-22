using System.Text;

namespace Talk.Web.Services;

public static class StringOutput
{
    public static string Print(System.Data.DataTable dt)
    {
        var sb = new StringBuilder();

        if (dt.Rows.Count == 1 && dt.Columns.Count == 1)
        {
            return dt.Rows[0][0] + "";
        }

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
            sb.Append(dt.Columns[i].ColumnName.PadRight(columnWidths[i] + 2));
        }

        sb.AppendLine();

        // Print separator line
        for (int i = 0; i < dt.Columns.Count; i++)
        {
            sb.Append(new string('-', columnWidths[i] + 2));
        }

        sb.AppendLine();

        // Print data rows
        foreach (System.Data.DataRow row in dt.Rows)
        {
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                string value = row[i]?.ToString() ?? "";
                sb.Append(value.PadRight(columnWidths[i] + 2));
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}