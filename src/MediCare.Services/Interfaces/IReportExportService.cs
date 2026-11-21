using MediCare.Services.Admin;

namespace MediCare.Services.Interfaces;

/// <summary>
/// Service for exporting daily clinic reports to PDF and Excel (.xlsx) formats (FR-35).
/// </summary>
public interface IReportExportService
{
    /// <summary>
    /// Exports the daily report metrics to an Excel (.xlsx) file.
    /// </summary>
    Task<byte[]> ExportDailyReportToExcelAsync(DailyReportDto report, CancellationToken ct = default);

    /// <summary>
    /// Exports the daily report metrics to a styled PDF document.
    /// </summary>
    Task<byte[]> ExportDailyReportToPdfAsync(DailyReportDto report, CancellationToken ct = default);
}
