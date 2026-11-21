using System.Diagnostics;
using MediCare.Web.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MediCare.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode = null)
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

        if (exceptionFeature?.Error != null)
        {
            _logger.LogError(exceptionFeature.Error,
                "Unhandled application exception occurred at path: {Path}. RequestId: {RequestId}",
                exceptionFeature.Path, requestId);
        }
        else if (statusCode.HasValue && statusCode.Value >= 400)
        {
            _logger.LogWarning("HTTP {StatusCode} error encountered. RequestId: {RequestId}", statusCode.Value, requestId);
        }

        var model = new ErrorViewModel
        {
            RequestId = requestId
        };

        ViewData["StatusCode"] = statusCode ?? HttpContext.Response.StatusCode;

        return View(model);
    }
}
