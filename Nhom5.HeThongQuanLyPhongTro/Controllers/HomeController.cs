using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Nhom5.HeThongQuanLyPhongTro.Models;

namespace Nhom5.HeThongQuanLyPhongTro.Controllers;

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
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
