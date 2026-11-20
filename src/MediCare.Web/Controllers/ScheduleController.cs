using System.Globalization;
using MediCare.Services.Interfaces;
using MediCare.Services.Schedule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediCare.Web.Controllers;

[Authorize]
public class ScheduleController : Controller
{
    private readonly IScheduleService _scheduleService;
    private readonly ILogger<ScheduleController> _logger;

    public ScheduleController(
        IScheduleService scheduleService,
        ILogger<ScheduleController> logger)
    {
        _scheduleService = scheduleService;
        _logger = logger;
    }

    /// <summary>
    /// FullCalendar JSON endpoint for querying unbooked, valid future appointment slots.
    /// GET /Schedule/FreeSlots?doctorId=&start=&end=
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> FreeSlots(int doctorId, string? start, string? end)
    {
        if (doctorId <= 0)
        {
            return BadRequest("DoctorId is required.");
        }

        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);

        DateOnly fromDate = today;
        DateOnly toDate = today.AddDays(14);

        if (!string.IsNullOrWhiteSpace(start) && TryParseDateString(start, out var parsedStart))
        {
            fromDate = parsedStart;
        }

        if (!string.IsNullOrWhiteSpace(end) && TryParseDateString(end, out var parsedEnd))
        {
            toDate = parsedEnd;
        }

        // Limit range to maximum 60 days to prevent excessive load
        if (toDate < fromDate) toDate = fromDate;
        if ((toDate.DayNumber - fromDate.DayNumber) > 60)
        {
            toDate = fromDate.AddDays(60);
        }

        var slots = await _scheduleService.GetFreeSlotsAsync(doctorId, fromDate, toDate);

        var events = slots.Select(s => new
        {
            id = s.SlotId,
            title = $"Available ({s.SlotMinutes}m)",
            start = $"{s.Date:yyyy-MM-dd}T{s.StartTime:hh\\:mm\\:ss}",
            end = $"{s.Date:yyyy-MM-dd}T{s.EndTime:hh\\:mm\\:ss}",
            backgroundColor = "#E8F5E9",
            borderColor = "#388E3C",
            textColor = "#388E3C",
            display = "block",
            extendedProps = new
            {
                date = s.Date.ToString("yyyy-MM-dd"),
                startTime = s.StartTime.ToString(@"hh\:mm"),
                endTime = s.EndTime.ToString(@"hh\:mm"),
                slotMinutes = s.SlotMinutes,
                formattedTime = s.FormattedTime
            }
        });

        return Json(events);
    }

    private static bool TryParseDateString(string input, out DateOnly date)
    {
        // Handle ISO strings (e.g. 2026-11-01T00:00:00Z) or YYYY-MM-DD
        if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        if (DateOnly.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        date = default;
        return false;
    }
}
