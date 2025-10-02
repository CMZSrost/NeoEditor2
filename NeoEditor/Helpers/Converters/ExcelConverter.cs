using System.Data;
using System.IO;
using Spire.Xls;

namespace NeoEditor.Helpers.Converters;

public static class ExcelConverter
{
    public static void DataSetToExcel(DataSet ds, string filePath)
    {
        var workbook = new Workbook();
        workbook.Worksheets.Clear();
        foreach (DataTable dt in ds.Tables)
        {
            var worksheet = workbook.Worksheets.Add(dt.TableName);
            worksheet.InsertDataTable(dt, true, 1, 1);
        }

        if (File.Exists(filePath)) //存在则删除
            File.Delete(filePath);

        workbook.SaveToFile(filePath);
    }
}