using MediCare.Services.Admin;

namespace MediCare.Services.Interfaces;

public interface IReportService
{
    /// <summary>
    /// Computes daily operational report for a chosen date (default today).
    /// Returns 0 for any metric if no activity occurred that day (FR-30).
    /// </summary>
    Task<DailyReportDto> GetDailyReportAsync(DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// Aggregates real-time clinic overview and performance data for the Admin Dashboard (Chart.js ready).
    /// </summary>
    Task<AdminDashboardDto> GetAdminDashboardDataAsync(CancellationToken ct = default);

    /// <summary>
    /// Computes full analytics dataset including 12-month appointment trends, doctor performance,
    /// specialization popularity, and patient growth (NFR-02: grouped DB queries).
    /// </summary>
    Task<AnalyticsReportDto> GetAnalyticsReportAsync(DateOnly? date = null, CancellationToken ct = default);
}
