using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Data;
using RoomRentalManagement.Models;

namespace RoomRentalManagement.Controllers
{
    // Admin quan ly tat ca yeu cau; Tenant chi xem va tao yeu cau cua chinh minh.
    [Authorize(Roles = "Admin,Tenant")]
    public class RequestsController : Controller
    {
        private readonly ApplicationDbContext _context;

        private static readonly string[] AllowedStatuses =
        {
            "Pending",
            "In Progress",
            "Resolved",
            "Rejected"
        };

        public RequestsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Requests
        public async Task<IActionResult> Index()
        {
            var query = _context.Requests
                .AsNoTracking()
                .Include(r => r.Tenant)
                .AsQueryable();

            if (!User.IsInRole("Admin"))
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null) return Forbid();

                query = query.Where(r => r.TenantID == tenant.TenantID);
            }

            var requests = await query
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            return View(requests);
        }

        // GET: /Requests/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var query = _context.Requests
                .AsNoTracking()
                .Include(r => r.Tenant)
                .Where(r => r.RequestID == id.Value);

            if (!User.IsInRole("Admin"))
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null) return Forbid();
                query = query.Where(r => r.TenantID == tenant.TenantID);
            }

            var request = await query.FirstOrDefaultAsync();
            if (request == null) return NotFound();

            return View(request);
        }

        // GET: /Requests/Create
        public async Task<IActionResult> Create()
        {
            var request = new Request
            {
                CreatedDate = DateTime.Now,
                Status = "Pending"
            };

            if (User.IsInRole("Admin"))
            {
                await LoadAdminOptionsAsync(request.TenantID, request.Status);
            }
            else
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null) return Forbid();
                request.TenantID = tenant.TenantID;
            }

            return View(request);
        }

        // POST: /Requests/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("TenantID,Content,CreatedDate,Status,Response")] Request request)
        {
            request.Tenant = null;

            if (User.IsInRole("Admin"))
            {
                ValidateRequest(request, allowAdminFields: true);

                var tenantExists = request.TenantID > 0 &&
                    await _context.Tenants.AnyAsync(t => t.TenantID == request.TenantID);

                if (!tenantExists)
                    ModelState.AddModelError(nameof(request.TenantID), "Vui lòng chọn người thuê hợp lệ.");

                if (request.CreatedDate == default)
                    request.CreatedDate = DateTime.Now;
            }
            else
            {
                var tenant = await GetCurrentTenantAsync();
                if (tenant == null) return Forbid();

                // Do not trust TenantID, Status, CreatedDate or Response sent by the browser.
                request.TenantID = tenant.TenantID;
                request.CreatedDate = DateTime.Now;
                request.Status = "Pending";
                request.Response = null;
                ValidateRequest(request, allowAdminFields: false);
            }

            if (!ModelState.IsValid)
            {
                if (User.IsInRole("Admin"))
                    await LoadAdminOptionsAsync(request.TenantID, request.Status);
                return View(request);
            }

            request.Content = request.Content.Trim();
            request.Status = User.IsInRole("Admin") ? NormalizeStatus(request.Status) : "Pending";
            request.Response = User.IsInRole("Admin") && !string.IsNullOrWhiteSpace(request.Response)
                ? request.Response.Trim()
                : null;

            _context.Requests.Add(request);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã tạo yêu cầu thành công.";
            return RedirectToAction(nameof(Index));
        }

        // Chi Admin moi duoc thay doi trang thai, phan hoi va nguoi gui.
        [Authorize(Roles = "Admin")]
        // GET: /Requests/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var request = await _context.Requests.FindAsync(id.Value);
            if (request == null) return NotFound();

            await LoadAdminOptionsAsync(request.TenantID, request.Status);
            return View(request);
        }

        // POST: /Requests/Edit/5
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("RequestID,TenantID,Content,CreatedDate,Status,Response")] Request input)
        {
            if (id != input.RequestID) return NotFound();

            input.Tenant = null;
            ValidateRequest(input, allowAdminFields: true);

            if (input.TenantID <= 0 ||
                !await _context.Tenants.AnyAsync(t => t.TenantID == input.TenantID))
            {
                ModelState.AddModelError(nameof(input.TenantID), "Vui lòng chọn người thuê hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAdminOptionsAsync(input.TenantID, input.Status);
                return View(input);
            }

            var existing = await _context.Requests
                .FirstOrDefaultAsync(r => r.RequestID == id);
            if (existing == null) return NotFound();

            existing.TenantID = input.TenantID;
            existing.Content = input.Content.Trim();
            existing.CreatedDate = input.CreatedDate;
            existing.Status = NormalizeStatus(input.Status);
            existing.Response = string.IsNullOrWhiteSpace(input.Response) ? null : input.Response.Trim();

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật yêu cầu.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        // GET: /Requests/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var request = await _context.Requests
                .AsNoTracking()
                .Include(r => r.Tenant)
                .FirstOrDefaultAsync(r => r.RequestID == id.Value);

            if (request == null) return NotFound();
            return View(request);
        }

        // POST: /Requests/Delete/5
        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var request = await _context.Requests.FindAsync(id);
            if (request == null) return NotFound();

            _context.Requests.Remove(request);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa yêu cầu.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<Tenant?> GetCurrentTenantAsync()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId)) return null;

            return await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserID == userId);
        }

        private void ValidateRequest(Request request, bool allowAdminFields)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                ModelState.AddModelError(nameof(request.Content), "Nội dung yêu cầu không được để trống.");
            else if (request.Content.Length > 2000)
                ModelState.AddModelError(nameof(request.Content), "Nội dung không được vượt quá 2000 ký tự.");

            if (allowAdminFields)
            {
                if (request.CreatedDate > DateTime.Now.AddMinutes(5))
                    ModelState.AddModelError(nameof(request.CreatedDate), "Ngày tạo không được nằm trong tương lai.");

                if (string.IsNullOrWhiteSpace(request.Status) ||
                    !AllowedStatuses.Contains(request.Status.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(request.Status), "Trạng thái yêu cầu không hợp lệ.");
                }

                if (request.Response?.Length > 2000)
                    ModelState.AddModelError(nameof(request.Response), "Phản hồi không được vượt quá 2000 ký tự.");
            }
        }

        private async Task LoadAdminOptionsAsync(int? selectedTenantId, string? selectedStatus)
        {
            var tenants = await _context.Tenants
                .AsNoTracking()
                .OrderBy(t => t.FullName)
                .ToListAsync();

            ViewBag.TenantOptions = tenants.Select(t => new SelectListItem
            {
                Value = t.TenantID.ToString(),
                Text = $"{t.FullName} (#{t.TenantID})",
                Selected = selectedTenantId == t.TenantID
            }).ToList();

            ViewBag.StatusOptions = AllowedStatuses.Select(status => new SelectListItem
            {
                Value = status,
                Text = status,
                Selected = string.Equals(status, selectedStatus, StringComparison.OrdinalIgnoreCase)
            }).ToList();
        }

        private static string NormalizeStatus(string? status)
        {
            var normalized = (status ?? string.Empty).Trim();
            return AllowedStatuses.FirstOrDefault(s =>
                string.Equals(s, normalized, StringComparison.OrdinalIgnoreCase)) ?? "Pending";
        }
    }
}
