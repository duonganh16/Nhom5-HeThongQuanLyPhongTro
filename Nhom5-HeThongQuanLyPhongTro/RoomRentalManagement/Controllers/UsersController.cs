
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Data;
using RoomRentalManagement.Models;

namespace RoomRentalManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<User> _hasher = new();

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Xem tất cả tài khoản
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Username)
                .ToListAsync();

            return View(users);
        }

        // Xem danh sách chủ nhà
        [HttpGet]
        public async Task<IActionResult> Landlords()
        {
            var landlords = await _context.Users
                .Where(u => u.Role == "Landlord")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return View(landlords);
        }

        // Xem tài khoản chờ duyệt
        [HttpGet]
        public async Task<IActionResult> Pending()
        {
            var users = await _context.Users
                .Where(u => u.Status == "Pending")
                .OrderBy(u => u.Username)
                .ToListAsync();

            return View(users);
        }

        // Duyệt tài khoản
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserID == id);

            if (user == null)
                return NotFound();

            if (user.Role != "Landlord" && user.Role != "Tenant")
                return BadRequest();

            if (user.Status != "Pending")
                return BadRequest();

            user.Status = "Active";

            await _context.SaveChangesAsync();

            TempData["Message"] = "Đã duyệt tài khoản thành công.";

            return RedirectToAction(nameof(Pending));
        }

        // Từ chối tài khoản
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserID == id);

            if (user == null)
                return NotFound();

            if (user.Role != "Landlord" && user.Role != "Tenant")
                return BadRequest();

            if (user.Status != "Pending")
                return BadRequest();

            user.Status = "Rejected";

            await _context.SaveChangesAsync();

            TempData["Message"] = "Đã từ chối tài khoản đăng ký.";

            return RedirectToAction(nameof(Pending));
        }

        // Hiển thị trang tạo tài khoản
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // Tạo tài khoản trực tiếp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string username,
            string fullName,
            string password,
            string role)
        {
            username = username?.Trim() ?? "";
            fullName = fullName?.Trim() ?? "";
            role = role?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(password) ||
                password.Length < 10 ||
                (role != "Landlord" && role != "Tenant"))
            {
                ViewBag.Error =
                    "Nhập đủ thông tin, mật khẩu ít nhất 10 ký tự và chọn đúng vai trò.";

                return View();
            }

            if (await _context.Users.AnyAsync(
                u => u.Username == username))
            {
                ViewBag.Error = "Tên đăng nhập đã tồn tại.";

                return View();
            }

            var user = new User
            {
                Username = username,
                FullName = fullName,
                Role = role,
                Status = "Active"
            };

            user.PasswordHash =
                _hasher.HashPassword(user, password);

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            TempData["Message"] = "Tạo tài khoản thành công.";

            return RedirectToAction(nameof(Index));
        }
    }
}
