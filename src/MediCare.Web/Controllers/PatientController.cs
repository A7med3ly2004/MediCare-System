using MediCare.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediCare.Web.Controllers;

[Authorize(Roles = "Patient")]
public class PatientController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        // Redirect patient to the doctors directory / search page (the main patient entry point)
        return RedirectToAction("Index", "Doctors");
    }

    [HttpGet]
    public IActionResult Appointments() => RedirectToAction("Index", "Doctors");

    [HttpGet]
    public IActionResult Doctors() => RedirectToAction("Index", "Doctors");

    [HttpGet]
    public IActionResult Health() => RedirectToAction("Index", "Doctors");

    [HttpGet]
    public IActionResult Profile() => RedirectToAction("Index", "Doctors");

    [HttpGet]
    public IActionResult Notifications() => RedirectToAction("Index", "Doctors");

    [HttpGet]
    public IActionResult Settings() => RedirectToAction("Index", "Doctors");
}
