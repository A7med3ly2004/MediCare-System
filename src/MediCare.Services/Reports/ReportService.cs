using System.Globalization;
using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Services.Admin;
using MediCare.Services.Appointments;
using MediCare.Services.Interfaces;
using MediCare.Services.Schedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MediCare.Services.Reports;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ReportService> _logger;

    public ReportService(
        ApplicationDbContext dbContext,
        ILogger<ReportService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DailyReportDto> GetDailyReportAsync(DateOnly date, CancellationToken ct = default)
    {
        var dayStartUtc = date.ToDateTime(TimeOnly.MinValue);
        var dayEndUtc = date.ToDateTime(TimeOnly.MaxValue);

        // 1. New patient registrations on that date
        var newPatients = await _dbContext.Patients
            .AsNoTracking()
            .CountAsync(p => p.User.CreatedAt >= dayStartUtc && p.User.CreatedAt <= dayEndUtc, ct);

        // 2. New doctor registrations on that date
        var newDoctors = await _dbContext.Doctors
            .AsNoTracking()
            .CountAsync(d => d.User.CreatedAt >= dayStartUtc && d.User.CreatedAt <= dayEndUtc, ct);

        // 3. Appointments booked on that date
        var appointmentsBooked = await _dbContext.Appointments
            .AsNoTracking()
            .CountAsync(a => a.CreatedAt >= dayStartUtc && a.CreatedAt <= dayEndUtc, ct);

        // 4. Appointments confirmed on that date (AuditLog, with fallback to appointment status on that date)
        var appointmentsConfirmed = await _dbContext.AuditLogs
            .AsNoTracking()
            .CountAsync(l => l.Action == "Confirm" && l.EntityName == "Appointment" && l.OccurredAt >= dayStartUtc && l.OccurredAt <= dayEndUtc, ct);

        if (appointmentsConfirmed == 0)
        {
            appointmentsConfirmed = await _dbContext.Appointments
                .AsNoTracking()
                .CountAsync(a => (a.CreatedAt >= dayStartUtc && a.CreatedAt <= dayEndUtc || a.AppointmentDate == date) && a.Status == AppointmentStatus.Confirmed, ct);
        }

        // 5. Appointments cancelled on that date (AuditLog, with fallback to appointment status on that date)
        var appointmentsCancelled = await _dbContext.AuditLogs
            .AsNoTracking()
            .CountAsync(l => l.Action == "Cancel" && l.EntityName == "Appointment" && l.OccurredAt >= dayStartUtc && l.OccurredAt <= dayEndUtc, ct);

        if (appointmentsCancelled == 0)
        {
            appointmentsCancelled = await _dbContext.Appointments
                .AsNoTracking()
                .CountAsync(a => (a.CreatedAt >= dayStartUtc && a.CreatedAt <= dayEndUtc || a.AppointmentDate == date) && a.Status == AppointmentStatus.Cancelled, ct);
        }

        // 6. Consultations completed on that date
        var completedConsultations = await _dbContext.Appointments
            .AsNoTracking()
            .CountAsync(a => a.AppointmentDate == date && a.Status == AppointmentStatus.Completed, ct);

        // 7. Estimated Revenue on that date (Sum of ConsultationFee of completed visits)
        var estimatedRevenue = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.AppointmentDate == date && a.Status == AppointmentStatus.Completed)
            .SumAsync(a => (decimal?)a.Doctor.ConsultationFee, ct) ?? 0m;

        return new DailyReportDto
        {
            Date = date,
            NewPatientRegistrations = newPatients,
            NewDoctorRegistrations = newDoctors,
            AppointmentsBooked = appointmentsBooked,
            AppointmentsConfirmed = appointmentsConfirmed,
            AppointmentsCancelled = appointmentsCancelled,
            CompletedConsultations = completedConsultations,
            EstimatedRevenue = estimatedRevenue
        };
    }

    public async Task<AdminDashboardDto> GetAdminDashboardDataAsync(CancellationToken ct = default)
    {
        var clinicNow = ScheduleService.GetClinicLocalNow();
        var today = DateOnly.FromDateTime(clinicNow);
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var monthStartUtc = currentMonthStart.ToDateTime(TimeOnly.MinValue);

        // Patient counts
        var totalPatients = await _dbContext.Patients.CountAsync(p => !p.IsDeleted, ct);
        var activePatients = await _dbContext.Patients.CountAsync(p => !p.IsDeleted && !p.User.IsSuspended, ct);
        var newPatientsThisMonth = await _dbContext.Patients.CountAsync(p => !p.IsDeleted && p.User.CreatedAt >= monthStartUtc, ct);

        // Doctor counts
        var totalDoctors = await _dbContext.Doctors.CountAsync(d => !d.IsDeleted, ct);

        var todayDayOfWeek = (int)today.DayOfWeek;
        var availableDoctors = await _dbContext.Doctors
            .AsNoTracking()
            .Where(d => !d.IsDeleted && !d.User.IsSuspended)
            .Where(d => d.Schedules.Any(s => s.DayOfWeek == todayDayOfWeek))
            .Where(d => !d.VacationDays.Any(v => v.VacationDate == today))
            .CountAsync(ct);

        // Today's appointments
        var todayAppts = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Where(a => a.AppointmentDate == today)
            .OrderBy(a => a.StartTime)
            .ToListAsync(ct);

        var todayApptsCount = todayAppts.Count(a => a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rejected);
        var todayCompleted = todayAppts.Count(a => a.Status == AppointmentStatus.Completed);
        var todayRemaining = todayAppts.Count(a => a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Pending);

        // Monthly completed stats
        var monthlyCompletedAppointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.AppointmentDate >= currentMonthStart && a.Status == AppointmentStatus.Completed)
            .Select(a => a.Doctor.ConsultationFee)
            .ToListAsync(ct);

        var monthlyRevenue = monthlyCompletedAppointments.Sum();
        var monthlyVisits = monthlyCompletedAppointments.Count;

        // Pending confirmations
        var pendingCount = await _dbContext.Appointments.CountAsync(a => a.Status == AppointmentStatus.Pending, ct);

        // Activity for last 7 days (line chart)
        var activityStartDate = today.AddDays(-6);
        var sevenDaysAppts = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.AppointmentDate >= activityStartDate && a.AppointmentDate <= today)
            .GroupBy(a => a.AppointmentDate)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, ct);

        var activityDays = new List<string>();
        var activityCounts = new List<int>();
        for (int i = 0; i < 7; i++)
        {
            var d = activityStartDate.AddDays(i);
            activityDays.Add(d.ToString("ddd", CultureInfo.InvariantCulture));
            activityCounts.Add(sevenDaysAppts.TryGetValue(d, out var count) ? count : 0);
        }

        // Weekly schedule distribution (bar chart: Mon-Sun)
        // Find Monday of the current week
        int diff = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var monday = today.AddDays(-diff);
        var weekEnd = monday.AddDays(6);

        var weekAppts = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.AppointmentDate >= monday && a.AppointmentDate <= weekEnd)
            .GroupBy(a => a.AppointmentDate)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, ct);

        var weeklyDays = new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        var weeklyCounts = new List<int>();
        for (int i = 0; i < 7; i++)
        {
            var d = monday.AddDays(i);
            weeklyCounts.Add(weekAppts.TryGetValue(d, out var count) ? count : 0);
        }

        // Upcoming appointments preview
        var upcomingDtos = todayAppts
            .Where(a => a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rejected)
            .Take(10)
            .Select(a => new AppointmentListItemDto
            {
                Id = a.Id,
                PatientId = a.PatientId,
                PatientName = a.Patient?.User?.FullName ?? "Patient",
                PatientCode = a.Patient?.PatientCode ?? string.Empty,
                DoctorId = a.DoctorId,
                DoctorName = a.Doctor?.User?.FullName ?? "Doctor",
                AppointmentDate = a.AppointmentDate,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                Status = a.Status,
                Notes = a.Notes
            })
            .ToList();

        // Doctor availability list
        var doctors = await _dbContext.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .Include(d => d.Specialization)
            .Include(d => d.Schedules)
            .Include(d => d.VacationDays.Where(v => v.VacationDate == today))
            .Where(d => !d.IsDeleted)
            .OrderBy(d => d.User.FullName)
            .Take(6)
            .ToListAsync(ct);

        var doctorAvailabilities = new List<DoctorAvailabilityItemDto>();
        foreach (var d in doctors)
        {
            var todaySched = d.Schedules.FirstOrDefault(s => s.DayOfWeek == todayDayOfWeek);
            bool isVacation = d.VacationDays.Any();
            bool isAvailable = todaySched != null && !isVacation && !d.User.IsSuspended;

            string workingHoursText;
            if (d.User.IsSuspended)
            {
                workingHoursText = "Suspended";
            }
            else if (isVacation)
            {
                workingHoursText = "On Vacation";
            }
            else if (todaySched != null)
            {
                workingHoursText = $"Available {todaySched.StartTime:hh\\:mm}–{todaySched.EndTime:hh\\:mm}";
            }
            else
            {
                workingHoursText = "Off duty today";
            }

            doctorAvailabilities.Add(new DoctorAvailabilityItemDto
            {
                DoctorId = d.Id,
                DoctorName = d.User.FullName,
                Specialization = d.Specialization?.Name ?? "General Practice",
                IsAvailableToday = isAvailable,
                WorkingHoursText = workingHoursText
            });
        }

        return new AdminDashboardDto
        {
            TotalPatients = totalPatients,
            ActivePatients = activePatients,
            NewPatientsThisMonth = newPatientsThisMonth,
            TodayAppointmentsCount = todayApptsCount,
            TodayCompletedCount = todayCompleted,
            TodayRemainingCount = todayRemaining,
            AvailableDoctorsCount = availableDoctors,
            TotalDoctorsCount = totalDoctors,
            MonthlyEstimatedRevenue = monthlyRevenue,
            MonthlyCompletedVisits = monthlyVisits,
            PendingConfirmationsCount = pendingCount,
            ActivityDays = activityDays,
            ActivityCounts = activityCounts,
            WeeklyDays = weeklyDays,
            WeeklyCounts = weeklyCounts,
            UpcomingAppointments = upcomingDtos,
            DoctorAvailabilities = doctorAvailabilities
        };
    }

    public async Task<AnalyticsReportDto> GetAnalyticsReportAsync(DateOnly? date = null, CancellationToken ct = default)
    {
        var clinicNow = ScheduleService.GetClinicLocalNow();
        var selectedDate = date ?? DateOnly.FromDateTime(clinicNow);

        var dailySummary = await GetDailyReportAsync(selectedDate, ct);

        // 12-month appointment trends (grouped DB query, NFR-02)
        var twelveMonthsAgo = selectedDate.AddMonths(-11);
        var dateThreshold = new DateOnly(twelveMonthsAgo.Year, twelveMonthsAgo.Month, 1);

        var monthlyAppointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.AppointmentDate >= dateThreshold)
            .GroupBy(a => new { a.AppointmentDate.Year, a.AppointmentDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                TotalBooked = g.Count(),
                Completed = g.Count(a => a.Status == AppointmentStatus.Completed),
                Cancelled = g.Count(a => a.Status == AppointmentStatus.Cancelled),
                Revenue = g.Where(a => a.Status == AppointmentStatus.Completed).Sum(a => a.Doctor.ConsultationFee)
            })
            .ToListAsync(ct);

        var monthlyTrends = new List<MonthlyTrendDto>();
        for (int i = 0; i < 12; i++)
        {
            var target = dateThreshold.AddMonths(i);
            var found = monthlyAppointments.FirstOrDefault(m => m.Year == target.Year && m.Month == target.Month);

            monthlyTrends.Add(new MonthlyTrendDto
            {
                Month = target.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                Year = target.Year,
                MonthNumber = target.Month,
                TotalBooked = found?.TotalBooked ?? 0,
                Completed = found?.Completed ?? 0,
                Cancelled = found?.Cancelled ?? 0,
                Revenue = found?.Revenue ?? 0m
            });
        }

        // Doctor performance (grouped DB query)
        var doctorPerformances = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.AppointmentDate >= dateThreshold)
            .GroupBy(a => new
            {
                a.DoctorId,
                DoctorName = a.Doctor.User.FullName,
                Specialization = a.Doctor.Specialization != null ? a.Doctor.Specialization.Name : "General Practice",
                a.Doctor.ConsultationFee
            })
            .Select(g => new DoctorPerformanceDto
            {
                DoctorId = g.Key.DoctorId,
                DoctorName = g.Key.DoctorName,
                Specialization = g.Key.Specialization,
                TotalAssigned = g.Count(a => a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rejected),
                CompletedVisits = g.Count(a => a.Status == AppointmentStatus.Completed),
                Revenue = g.Where(a => a.Status == AppointmentStatus.Completed).Sum(a => g.Key.ConsultationFee)
            })
            .OrderByDescending(d => d.CompletedVisits)
            .Take(8)
            .ToListAsync(ct);

        // Popular specializations (grouped DB query)
        var popularSpecializations = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.AppointmentDate >= dateThreshold && a.Doctor.Specialization != null)
            .GroupBy(a => new { a.Doctor.SpecializationId, a.Doctor.Specialization!.Name })
            .Select(g => new SpecializationPopularityDto
            {
                SpecializationId = g.Key.SpecializationId,
                SpecializationName = g.Key.Name,
                AppointmentCount = g.Count(),
                DoctorCount = _dbContext.Doctors.Count(d => d.SpecializationId == g.Key.SpecializationId && !d.IsDeleted)
            })
            .OrderByDescending(s => s.AppointmentCount)
            .Take(6)
            .ToListAsync(ct);

        // Patient growth trend (last 6 months, grouped DB query)
        var sixMonthsAgo = selectedDate.AddMonths(-5);
        var sixMonthsThreshold = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var patientGroups = await _dbContext.Patients
            .AsNoTracking()
            .Where(p => p.User.CreatedAt >= sixMonthsThreshold)
            .GroupBy(p => new { p.User.CreatedAt.Year, p.User.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(ct);

        var patientGrowth = new List<PatientGrowthDto>();
        for (int i = 0; i < 6; i++)
        {
            var target = sixMonthsThreshold.AddMonths(i);
            var found = patientGroups.FirstOrDefault(p => p.Year == target.Year && p.Month == target.Month);

            patientGrowth.Add(new PatientGrowthDto
            {
                Month = target.ToString("MMM", CultureInfo.InvariantCulture),
                NewPatients = found?.Count ?? 0
            });
        }

        return new AnalyticsReportDto
        {
            SelectedDate = selectedDate,
            DailySummary = dailySummary,
            MonthlyTrends = monthlyTrends,
            DoctorPerformances = doctorPerformances,
            PopularSpecializations = popularSpecializations,
            PatientGrowth = patientGrowth
        };
    }
}
