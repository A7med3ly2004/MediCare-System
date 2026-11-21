using System.Collections.Concurrent;
using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Services.Appointments;
using MediCare.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediCare.Tests;

public class ConcurrencyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ConcurrencyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseSeeded();
    }

    [Fact]
    public async Task ConcurrentBooking_20ParallelRequestsForSameSlot_ExactlyOneSucceeds_OthersFailCleanly()
    {
        // Target a future date and a specific time slot on that date
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14));
        var startTime = new TimeSpan(10, 0, 0);
        var endTime = new TimeSpan(10, 30, 0);
        int doctorId = 1;

        int totalRequests = 20;
        var successList = new ConcurrentBag<AppointmentDetailDto>();
        var failureExceptions = new ConcurrentBag<Exception>();

        // Run 20 concurrent booking tasks simultaneously
        var tasks = Enumerable.Range(1, totalRequests).Select(async i =>
        {
            // Each request gets its own independent scope and DbContext (simulating concurrent web requests)
            using var scope = _factory.Services.CreateScope();
            var appointmentService = scope.ServiceProvider.GetRequiredService<IAppointmentService>();

            var dto = new BookAppointmentDto
            {
                DoctorId = doctorId,
                PatientUserId = "test-pat-1",
                AppointmentDate = targetDate,
                StartTime = startTime,
                EndTime = endTime,
                Notes = $"Concurrent booking attempt #{i}"
            };

            try
            {
                var result = await appointmentService.CreateAsync(dto);
                successList.Add(result);
            }
            catch (Exception ex)
            {
                failureExceptions.Add(ex);
            }
        });

        await Task.WhenAll(tasks);

        // 1. Assert exactly 1 booking succeeded (KPI-06)
        Assert.Single(successList);

        // 2. Assert exactly 19 requests failed cleanly
        Assert.Equal(totalRequests - 1, failureExceptions.Count);
        foreach (var ex in failureExceptions)
        {
            // Must fail cleanly with SlotUnavailableException or DbUpdateException
            Assert.True(
                ex is SlotUnavailableException || ex is DbUpdateException,
                $"Unexpected exception type: {ex.GetType().Name} - {ex.Message}");
        }

        // 3. Verify real SQL Server database: exactly 1 active appointment exists for that doctor & slot
        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var apptsInDb = await db.Appointments
            .Where(a => a.DoctorId == doctorId && a.AppointmentDate == targetDate && a.StartTime == startTime)
            .ToListAsync();

        Assert.Single(apptsInDb);
        Assert.Equal(AppointmentStatus.Pending, apptsInDb[0].Status);
    }
}
