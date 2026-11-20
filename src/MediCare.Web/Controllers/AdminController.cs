using MediCare.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediCare.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;

    public AdminController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Admin Overview";
        ViewData["PortalLabel"] = "Workspace";
        ViewData["PageLabel"] = "Overview";
        ViewData["ActiveNav"] = "overview";

        ViewBag.TotalPatients = await _context.Patients.CountAsync();
        ViewBag.TotalDoctors = await _context.Doctors.CountAsync();

        return View();
    }

    [HttpGet]
    public IActionResult Appointments() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Patients() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Doctors() => RedirectToAction("Index", "Doctors");

    [HttpGet]
    public IActionResult Schedule() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Reports() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Notifications() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Settings() => RedirectToAction(nameof(Index));
}
