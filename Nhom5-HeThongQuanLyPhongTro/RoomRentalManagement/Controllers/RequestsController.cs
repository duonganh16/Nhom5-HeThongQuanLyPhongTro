
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Data;
using RoomRentalManagement.Models;

namespace RoomRentalManagement.Controllers
{
    [Authorize(Roles = "Admin,Landlord,Tenant")]
    public class RequestsController : Controller
    {
        private readonly ApplicationDbContext _context;

        private static readonly string[] DefaultStatuses =
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

        private int CurrentUserID =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Lấy danh sách người thuê gắn với tài khoản đang đăng nhập.
        private IQueryable<Tenant> CurrentTenants()
        {
            return _context.Tenants.Where(t => t.UserID == CurrentUserID);
        }

        // Yêu cầu mà tài khoản hiện tại được phép xem.
        private IQueryable<Request> AccessibleRequests()
        {
            var query = _context.Requests.AsQueryable();

            if (User.IsInRole("Admin"))
                return query;

            if (User.IsInRole("Tenant"))
            {
                return query.Where(r =>
                    r.Tenant != null &&
                    r.Tenant.UserID == CurrentUserID);
            }

            if (User.IsInRole("Landlord"))
            {
                return query.Where(r =>
                    _context.Contracts.Any(c =>
                        c.TenantID == r.TenantID &&
                        c.Room != null &&
                        c.Room.LandlordID == CurrentUserID));
            }

            return query.Where(r => false);
        }

        public async Task<IActionResult> Index()
        {
            var requests = await AccessibleRequests()
                .AsNoTracking()
                .Include(r => r.Tenant)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            return View(requests);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await AccessibleRequests()
                .AsNoTracking()
                .Include(r => r.Tenant)
                .FirstOrDefaultAsync(r => r.RequestID == id.Value);

            if (request == null)
                return NotFound();

            return View(request);
        }

        // Admin, Chủ nhà và Người thuê đều có thể gửi yêu cầu.
        public async Task<IActionResult> Create()
        {
            var request = new Request
            {
                CreatedDate = DateTime.Now,
                Status = "Pending"
            };

            if (User.IsInRole("Tenant"))
            {
                var tenant = await CurrentTenants().FirstOrDefaultAsync();

                if (tenant == null)
                {
                    TempData["Error"] =
                        "Tài khoản chưa được liên kết với hồ sơ người thuê.";
                    return RedirectToAction(nameof(Index));
                }

                request.TenantID = tenant.TenantID;
            }

            await LoadOptionsAsync(request.TenantID, request.Status);
            return View(request);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("TenantID,Content,CreatedDate,Status,Response")]
            Request request)
        {
            if (User.IsInRole("Tenant"))
            {
                var tenant = await CurrentTenants().FirstOrDefaultAsync();

                if (tenant == null)
                    return Forbid();

                // Không cho người thuê gửi yêu cầu dưới tên người khác.
                request.TenantID = tenant.TenantID;
                request.Status = "Pending";
                request.Response = null;
                request.CreatedDate = DateTime.Now;
            }
            else
            {
                if (!await _context.Tenants.AnyAsync(
                    t => t.TenantID == request.TenantID))
                {
                    ModelState.AddModelError(
                        nameof(request.TenantID),
                        "Vui lòng chọn người thuê hợp lệ.");
                }

                if (User.IsInRole("Landlord") &&
                    !await _context.Contracts.AnyAsync(c =>
                        c.TenantID == request.TenantID &&
                        c.Room != null &&
                        c.Room.LandlordID == CurrentUserID))
                {
                    ModelState.AddModelError(
                        nameof(request.TenantID),
                        "Người thuê này không thuộc phòng bạn quản lý.");
                }

                request.Status = "Pending";
                request.Response = null;
                request.CreatedDate = DateTime.Now;
            }

            ValidateRequest(request);

            if (!ModelState.IsValid)
            {
                await LoadOptionsAsync(request.TenantID, request.Status);
                return View(request);
            }

            request.Content = request.Content.Trim();

            _context.Requests.Add(request);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã gửi yêu cầu thành công.";
            return RedirectToAction(nameof(Index));
        }

        // Chỉ Admin và Chủ nhà được cập nhật trạng thái/phản hồi.
        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await AccessibleRequests()
                .FirstOrDefaultAsync(r => r.RequestID == id.Value);

            if (request == null)
                return NotFound();

            await LoadOptionsAsync(request.TenantID, request.Status);
            return View(request);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("RequestID,Status,Response")] Request input)
        {
            if (id != input.RequestID)
                return NotFound();

            var request = await AccessibleRequests()
                .FirstOrDefaultAsync(r => r.RequestID == id);

            if (request == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(input.Status) ||
                !DefaultStatuses.Contains(input.Status.Trim()))
            {
                ModelState.AddModelError(
                    nameof(input.Status),
                    "Vui lòng chọn trạng thái hợp lệ.");
            }

            if (input.Response?.Length > 2000)
            {
                ModelState.AddModelError(
                    nameof(input.Response),
                    "Phản hồi không được vượt quá 2000 ký tự.");
            }

            if (!ModelState.IsValid)
            {
                await LoadOptionsAsync(request.TenantID, input.Status);
                return View(request);
            }

            request.Status = input.Status.Trim();
            request.Response = string.IsNullOrWhiteSpace(input.Response)
                ? null
                : input.Response.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật yêu cầu thành công.";
            return RedirectToAction(nameof(Index));
        }

        // Chỉ Admin được xóa yêu cầu.
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await AccessibleRequests()
                .AsNoTracking()
                .Include(r => r.Tenant)
                .FirstOrDefaultAsync(r => r.RequestID == id.Value);

            if (request == null)
                return NotFound();

            return View(request);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var request = await AccessibleRequests()
                .FirstOrDefaultAsync(r => r.RequestID == id);

            if (request == null)
                return NotFound();

            _context.Requests.Remove(request);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa yêu cầu.";
            return RedirectToAction(nameof(Index));
        }

        private void ValidateRequest(Request request)
        {
            if (request.TenantID <= 0)
            {
                ModelState.AddModelError(
                    nameof(request.TenantID),
                    "Vui lòng chọn người thuê.");
            }

            if (string.IsNullOrWhiteSpace(request.Content))
            {
                ModelState.AddModelError(
                    nameof(request.Content),
                    "Nội dung yêu cầu không được để trống.");
            }

            if (request.Content?.Length > 2000)
            {
                ModelState.AddModelError(
                    nameof(request.Content),
                    "Nội dung không được vượt quá 2000 ký tự.");
            }
        }

        private async Task LoadOptionsAsync(
            int? selectedTenantId,
            string? selectedStatus)
        {
            if (!User.IsInRole("Tenant"))
            {
                var tenants = _context.Tenants
                    .AsNoTracking()
                    .AsQueryable();

                if (User.IsInRole("Landlord"))
                {
                    tenants = tenants.Where(t =>
                        _context.Contracts.Any(c =>
                            c.TenantID == t.TenantID &&
                            c.Room != null &&
                            c.Room.LandlordID == CurrentUserID));
                }

                var tenantList = await tenants
                    .OrderBy(t => t.FullName)
                    .ToListAsync();

                ViewBag.TenantOptions = tenantList
                    .Select(t => new SelectListItem
                    {
                        Value = t.TenantID.ToString(),
                        Text = t.FullName,
                        Selected = selectedTenantId == t.TenantID
                    })
                    .ToList();
            }
            else
            {
                ViewBag.TenantOptions = new List<SelectListItem>();
            }

            ViewBag.StatusOptions = DefaultStatuses
                .Select(status => new SelectListItem
                {
                    Value = status,
                    Text = status,
                    Selected = string.Equals(
                        status,
                        selectedStatus,
                        StringComparison.OrdinalIgnoreCase)
                })
                .ToList();
        }
    }
}
