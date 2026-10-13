using Microsoft.AspNetCore.Mvc;

namespace Nhom5.HeThongQuanLyPhongTro.Controllers
{
    public class RoomsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}