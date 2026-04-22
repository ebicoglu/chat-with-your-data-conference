using System.Data;
using ClosedXML.Excel;

namespace Talk.Web.Services;

public static class ExcelService
{
    public static void GenerateExcel(DataTable data, string filePath)
    {
        Save(GenerateExcelBytes(data), filePath);
    }

    public static byte[] GenerateExcelBytes(DataTable data, string sheetName = "Report")
    {
        // ClosedXML requires a non-empty table name; give the DataTable one if missing.
        if (string.IsNullOrWhiteSpace(data.TableName))
        {
            data.TableName = sheetName;
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);
        worksheet.Cell(1, 1).InsertTable(data);
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Save(byte[] excel, string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        File.WriteAllBytes(filePath, excel);
    }
}