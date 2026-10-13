
using Microsoft.AspNetCore.Mvc;

namespace Nhom5.HeThongQuanLyPhongTro.Controllers
{
    public class RequestsController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Yêu cầu / Sự cố";
            return View();
        }
    }
}
