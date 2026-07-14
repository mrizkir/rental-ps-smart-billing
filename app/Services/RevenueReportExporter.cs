using ClosedXML.Excel;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Services;

public static class RevenueReportExporter
{
    public static void ExportToExcel(RevenueReportResult report, string filePath)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Pendapatan");

        sheet.Cell(1, 1).Value = "Laporan Pendapatan";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        sheet.Cell(2, 1).Value = "Periode";
        sheet.Cell(2, 2).Value = report.PeriodDisplay;
        sheet.Cell(3, 1).Value = "Jumlah Transaksi";
        sheet.Cell(3, 2).Value = report.SessionCount;
        sheet.Cell(4, 1).Value = "Total Pendapatan";
        sheet.Cell(4, 2).Value = report.TotalAmount;
        sheet.Cell(4, 2).Style.NumberFormat.Format = "Rp #,##0";

        const int headerRow = 6;
        string[] headers =
        [
            "No",
            "Selesai",
            "Mulai",
            "Unit TV",
            "Customer",
            "Paket",
            "Jumlah"
        ];

        for (var col = 0; col < headers.Length; col++)
        {
            var cell = sheet.Cell(headerRow, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#ECEFF1");
        }

        var row = headerRow + 1;
        var index = 1;
        foreach (var item in report.Items)
        {
            sheet.Cell(row, 1).Value = index++;
            sheet.Cell(row, 2).Value = item.EndedAt.ToLocalTime();
            sheet.Cell(row, 2).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            sheet.Cell(row, 3).Value = item.StartedAt.ToLocalTime();
            sheet.Cell(row, 3).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            sheet.Cell(row, 4).Value = item.TvName;
            sheet.Cell(row, 5).Value = item.CustomerDisplay;
            sheet.Cell(row, 6).Value = item.PackageDisplay;
            sheet.Cell(row, 7).Value = item.Amount;
            sheet.Cell(row, 7).Style.NumberFormat.Format = "Rp #,##0";
            row++;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }
}
