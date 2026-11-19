using MediCare.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediCare.Web.Controllers;

[Authorize]
public class DoctorsController : Controller
{
    private readonly ApplicationDbContext _context;

    public DoctorsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Doctors Directory";
        ViewData["PortalLabel"] = "Patient portal";
        ViewData["PageLabel"] = "Find a doctor";
        ViewData["ActiveNav"] = "doctors";

        var doctors = await _context.Doctors
            .AsNoTracking()
            .Include(d => d.User)
            .Include(d => d.Specialization)
            .Take(12)
            .ToListAsync();

        return View(doctors);
    }
}
