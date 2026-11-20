using System.Security.Claims;
using MediCare.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediCare.Web.Controllers;

[Authorize(Roles = "Doctor,Patient")]
public class PrescriptionController : Controller
{
    private readonly IMedicalRecordService _medicalRecordService;
    private readonly ILogger<PrescriptionController> _logger;

    public PrescriptionController(
        IMedicalRecordService medicalRecordService,
        ILogger<PrescriptionController> logger)
    {
        _medicalRecordService = medicalRecordService;
        _logger = logger;
    }

    /// <summary>
    /// Print-friendly prescription view. Clean printable format with clinic header, Rx logo,
    /// medication items, doctor signature line, and print CSS hiding navigation (FR-22).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Print(int id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId)) return Challenge();

        var role = User.IsInRole("Doctor") ? "Doctor" : "Patient";

        try
        {
            var record = await _medicalRecordService.GetRecordByIdAsync(id, currentUserId, role);
            if (record == null) return NotFound();

            return View(record);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
