using ClosedXML.Excel;
using MediCare.Services.Admin;
using MediCare.Services.Interfaces;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MediCare.Services.Reports;

public class ReportExportService : IReportExportService
{
    private readonly ILogger<ReportExportService> _logger;

    static ReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
    }

    public ReportExportService(ILogger<ReportExportService> logger)
    {
        _logger = logger;
    }

    public Task<byte[]> ExportDailyReportToExcelAsync(DailyReportDto report, CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Daily Report");

        // Styling palette
        var primaryColor = XLColor.FromHtml("#0284C7");
        var secondaryColor = XLColor.FromHtml("#F1F5F9");
        var darkSlate = XLColor.FromHtml("#0F172A");

        // Title Header
        worksheet.Cell(1, 1).Value = "MediCare Clinic - Daily Activity Report";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Cell(1, 1).Style.Font.FontColor = primaryColor;
        worksheet.Range(1, 1, 1, 4).Merge();

        // Subtitle / Date
        worksheet.Cell(2, 1).Value = $"Report Date: {report.Date:dddd, MMMM dd, yyyy}";
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Cell(2, 1).Style.Font.FontSize = 11;
        worksheet.Range(2, 1, 2, 4).Merge();

        worksheet.Cell(3, 1).Value = $"Generated At: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";
        worksheet.Cell(3, 1).Style.Font.FontSize = 9;
        worksheet.Cell(3, 1).Style.Font.FontColor = XLColor.DimGray;
        worksheet.Range(3, 1, 3, 4).Merge();

        // Table Header
        int headerRow = 5;
        worksheet.Cell(headerRow, 1).Value = "Metric";
        worksheet.Cell(headerRow, 2).Value = "Value";
        worksheet.Cell(headerRow, 3).Value = "Unit / Notes";

        var headerRange = worksheet.Range(headerRow, 1, headerRow, 3);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = primaryColor;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        // Data Rows
        var metrics = new (string Name, object Value, string Notes)[]
        {
            ("New Patient Registrations", report.NewPatientRegistrations, "Registered via patient portal"),
            ("New Doctor Registrations", report.NewDoctorRegistrations, "Staff accounts created"),
            ("Total Appointments Booked", report.AppointmentsBooked, "Appointments initiated on this date"),
            ("Appointments Confirmed", report.AppointmentsConfirmed, "Confirmed by clinic / doctor"),
            ("Appointments Cancelled", report.AppointmentsCancelled, "Cancelled by patient or clinic"),
            ("Completed Consultations", report.CompletedConsultations, "Clinical visits fulfilled"),
            ("Estimated Revenue", report.EstimatedRevenue, "EGP (completed appointments consultation fee)")
        };

        int currentRow = headerRow + 1;
        foreach (var m in metrics)
        {
            worksheet.Cell(currentRow, 1).Value = m.Name;
            if (m.Value is decimal dec)
            {
                worksheet.Cell(currentRow, 2).Value = dec;
                worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0.00 \"EGP\"";
            }
            else
            {
                worksheet.Cell(currentRow, 2).Value = Convert.ToInt32(m.Value);
                worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = "#,##0";
            }
            worksheet.Cell(currentRow, 3).Value = m.Notes;

            if (currentRow % 2 == 1)
            {
                worksheet.Range(currentRow, 1, currentRow, 3).Style.Fill.BackgroundColor = secondaryColor;
            }

            currentRow++;
        }

        // Summary KPI Box
        int summaryStart = currentRow + 2;
        worksheet.Cell(summaryStart, 1).Value = "Summary KPI Overview";
        worksheet.Cell(summaryStart, 1).Style.Font.Bold = true;
        worksheet.Cell(summaryStart, 1).Style.Font.FontSize = 12;

        decimal completionRate = report.AppointmentsBooked > 0
            ? Math.Round((decimal)report.CompletedConsultations / report.AppointmentsBooked * 100m, 1)
            : 0m;

        worksheet.Cell(summaryStart + 1, 1).Value = "Fulfillment Rate";
        worksheet.Cell(summaryStart + 1, 2).Value = $"{completionRate}%";
        worksheet.Cell(summaryStart + 1, 3).Value = "Completed vs. Total Booked";

        // Auto-fit columns
        worksheet.Columns(1, 3).AdjustToContents();
        worksheet.Column(1).Width += 5;
        worksheet.Column(2).Width += 5;
        worksheet.Column(3).Width += 8;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    public Task<byte[]> ExportDailyReportToPdfAsync(DailyReportDto report, CancellationToken ct = default)
    {
        decimal completionRate = report.AppointmentsBooked > 0
            ? Math.Round((decimal)report.CompletedConsultations / report.AppointmentsBooked * 100m, 1)
            : 0m;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                // Header
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("MediCare Clinic").FontSize(20).Bold().FontColor("#0284C7");
                            titleCol.Item().Text("Daily Operational & Activity Report").FontSize(12).SemiBold().FontColor(Colors.Grey.Medium);
                        });

                        row.ConstantItem(180).AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text($"Date: {report.Date:yyyy-MM-dd}").Bold();
                            metaCol.Item().Text($"Day: {report.Date:dddd}").FontColor(Colors.Grey.Medium);
                            metaCol.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                // Content
                page.Content().Column(col =>
                {
                    col.Spacing(15);

                    // Top KPI Cards
                    col.Item().Row(row =>
                    {
                        row.Spacing(10);

                        row.RelativeItem().Background("#F0F9FF").Padding(12).Column(c =>
                        {
                            c.Item().Text("Total Booked").FontSize(9).FontColor("#0369A1");
                            c.Item().Text(report.AppointmentsBooked.ToString()).FontSize(18).Bold().FontColor("#0284C7");
                            c.Item().Text("Appointments").FontSize(8).FontColor(Colors.Grey.Medium);
                        });

                        row.RelativeItem().Background("#F0FDF4").Padding(12).Column(c =>
                        {
                            c.Item().Text("Completed").FontSize(9).FontColor("#15803D");
                            c.Item().Text(report.CompletedConsultations.ToString()).FontSize(18).Bold().FontColor("#16A34A");
                            c.Item().Text($"{completionRate}% completion rate").FontSize(8).FontColor(Colors.Grey.Medium);
                        });

                        row.RelativeItem().Background("#FEF2F2").Padding(12).Column(c =>
                        {
                            c.Item().Text("Cancelled").FontSize(9).FontColor("#B91C1C");
                            c.Item().Text(report.AppointmentsCancelled.ToString()).FontSize(18).Bold().FontColor("#DC2626");
                            c.Item().Text("Total cancellations").FontSize(8).FontColor(Colors.Grey.Medium);
                        });

                        row.RelativeItem().Background("#FAF5FF").Padding(12).Column(c =>
                        {
                            c.Item().Text("Est. Revenue").FontSize(9).FontColor("#7E22CE");
                            c.Item().Text($"{report.EstimatedRevenue:N0} EGP").FontSize(16).Bold().FontColor("#9333EA");
                            c.Item().Text("Consultation fees").FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });

                    // Detailed Metric Table
                    col.Item().Text("Daily Breakdown Details").FontSize(13).Bold().FontColor("#0F172A");

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(4);
                        });

                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Background("#0284C7").Padding(8).Text("Metric").Bold().FontColor(Colors.White);
                            header.Cell().Background("#0284C7").Padding(8).Text("Value").Bold().FontColor(Colors.White);
                            header.Cell().Background("#0284C7").Padding(8).Text("Description / Source").Bold().FontColor(Colors.White);
                        });

                        var rows = new (string Name, string Value, string Desc)[]
                        {
                            ("New Patients Registered", report.NewPatientRegistrations.ToString(), "New patient accounts registered on this day"),
                            ("New Doctors Registered", report.NewDoctorRegistrations.ToString(), "Medical staff accounts registered on this day"),
                            ("Appointments Booked", report.AppointmentsBooked.ToString(), "Total booking requests submitted on this day"),
                            ("Appointments Confirmed", report.AppointmentsConfirmed.ToString(), "Appointments confirmed by doctors or admin"),
                            ("Appointments Cancelled", report.AppointmentsCancelled.ToString(), "Appointments cancelled by patients or clinic"),
                            ("Consultations Completed", report.CompletedConsultations.ToString(), "Visits where medical records were saved"),
                            ("Estimated Revenue", $"{report.EstimatedRevenue:N2} EGP", "Cumulative consultation fees for completed visits"),
                            ("Fulfillment Rate", $"{completionRate}%", "Ratio of completed visits to total bookings")
                        };

                        for (int i = 0; i < rows.Length; i++)
                        {
                            var r = rows[i];
                            var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                            table.Cell().Background(bg).Padding(8).Text(r.Name).Medium();
                            table.Cell().Background(bg).Padding(8).Text(r.Value).Bold();
                            table.Cell().Background(bg).Padding(8).Text(r.Desc).FontColor(Colors.Grey.Medium);
                        }
                    });

                    // Clinic Notice / Footer note
                    col.Item().PaddingTop(15).Background("#F8FAFC").Padding(10).Column(c =>
                    {
                        c.Item().Text("Clinic Administrative Notice").Bold().FontSize(9);
                        c.Item().Text("This report is generated automatically by MediCare Clinic Management System and is intended for administrative review and record keeping.").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("MediCare Clinic Management System • Confidential").FontSize(8).FontColor(Colors.Grey.Medium);
                    row.ConstantItem(100).AlignRight().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
                });
            });
        });

        var pdfBytes = document.GeneratePdf();
        return Task.FromResult(pdfBytes);
    }
}
