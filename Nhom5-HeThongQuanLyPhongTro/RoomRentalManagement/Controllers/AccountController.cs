
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using RoomRentalManagement.Data;
using RoomRentalManagement.Models;

namespace RoomRentalManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<User> _hasher = new();

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập tên đăng nhập và mật khẩu.";
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Username == username.Trim());

            if (user == null ||
                _hasher.VerifyHashedPassword(
                    user, user.PasswordHash, password)
                == PasswordVerificationResult.Failed)
            {
                ViewBag.Error = "Tên đăng nhập hoặc mật khẩu không đúng.";
                return View();
            }

            if (user.Status != "Active")
            {
                ViewBag.Error = user.Status == "Pending"
                    ? "Tài khoản đang chờ Admin duyệt."
                    : "Tài khoản chưa được kích hoạt hoặc đã bị từ chối.";
                return View();
            }

            var role = user.Role.Trim();

            if (!new[] { "Admin", "Landlord", "Tenant" }
                .Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                ViewBag.Error = "Tài khoản chưa được gán vai trò hợp lệ.";
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.GivenName, user.FullName),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string fullName,
            string username,
            string password,
            string confirmPassword,
            string role)
        {
            fullName = fullName?.Trim() ?? "";
            username = username?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin.";
                return View();
            }

            if (password.Length < 10)
            {
                ViewBag.Error = "Mật khẩu phải có ít nhất 10 ký tự.";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu xác nhận không khớp.";
                return View();
            }

            // Không cho khách tự đăng ký tài khoản Admin.
            if (role != "Landlord" && role != "Tenant")
            {
                ViewBag.Error = "Vui lòng chọn Chủ nhà hoặc Người thuê.";
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
                Status = "Pending"
            };

            user.PasswordHash = _hasher.HashPassword(user, password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(RegisterSuccess));
        }

        [HttpGet]
        public IActionResult RegisterSuccess()
        {
            return View();
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
