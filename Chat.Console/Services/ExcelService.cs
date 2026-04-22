using System.Data;
using Chat.Console.Utils;
using ClosedXML.Excel;

namespace Chat.Console.Services;

public static class ExcelService
{
    private static readonly string DefaultFilePath = Path.GetFullPath(Path.Combine("Output", "excel.xlsx"));

    public static string GenerateExcel(DataTable data)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Report");
        worksheet.Cell(1, 1).InsertTable(data);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        Helpers.SaveBytes(stream.ToArray(), DefaultFilePath);
        return DefaultFilePath;
    }
}