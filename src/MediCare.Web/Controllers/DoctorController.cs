using MediCare.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediCare.Web.Controllers;

[Authorize(Roles = "Doctor")]
public class DoctorController : Controller
{
    private readonly ApplicationDbContext _context;

    public DoctorController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Doctor Portal";
        ViewData["PortalLabel"] = "Doctor portal";
        ViewData["PageLabel"] = "Overview";
        ViewData["ActiveNav"] = "overview";

        // Fetch basic info for current doctor if exists
        var userEmail = User.Identity?.Name;
        var doctor = await _context.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .Include(d => d.Specialization)
            .FirstOrDefaultAsync(d => d.User.Email == userEmail);

        return View(doctor);
    }

    [HttpGet]
    public IActionResult Schedule() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Patients() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult MedicalRecords() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Messages() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Settings() => RedirectToAction(nameof(Index));
}
