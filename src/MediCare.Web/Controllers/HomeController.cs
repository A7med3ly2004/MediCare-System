using System.Diagnostics;
using MediCare.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace MediCare.Web.Controllers;

public class HomeController : Controller
{
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
        var model = new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        };

        ViewData["StatusCode"] = statusCode ?? HttpContext.Response.StatusCode;

        return View(model);
    }
}
