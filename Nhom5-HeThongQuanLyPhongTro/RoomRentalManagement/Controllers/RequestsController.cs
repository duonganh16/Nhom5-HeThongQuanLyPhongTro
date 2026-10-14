using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Data;
using RoomRentalManagement.Models;

namespace RoomRentalManagement.Controllers
{
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

        // GET: /Requests
        public async Task<IActionResult> Index()
        {
            var requests = await _context.Requests
                .AsNoTracking()
                .Include(r => r.Tenant)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            return View(requests);
        }

        // GET: /Requests/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await _context.Requests
                .AsNoTracking()
                .Include(r => r.Tenant)
                .FirstOrDefaultAsync(r => r.RequestID == id.Value);

            if (request == null)
                return NotFound();

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

            await LoadOptionsAsync(request.TenantID, request.Status);
            return View(request);
        }

        // POST: /Requests/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Request request)
        {
            request.Tenant = null;
            ValidateRequest(request);

            if (request.TenantID > 0 &&
                !await _context.Tenants.AnyAsync(t => t.TenantID == request.TenantID))
            {
                ModelState.AddModelError(nameof(request.TenantID), "Người thuê được chọn không tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                await LoadOptionsAsync(request.TenantID, request.Status);
                return View(request);
            }

            request.Content = request.Content.Trim();
            request.Status = string.IsNullOrWhiteSpace(request.Status)
                ? "Pending"
                : request.Status.Trim();

            if (request.CreatedDate == default)
                request.CreatedDate = DateTime.Now;

            _context.Requests.Add(request);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: /Requests/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await _context.Requests.FindAsync(id.Value);
            if (request == null)
                return NotFound();

            await LoadOptionsAsync(request.TenantID, request.Status);
            return View(request);
        }

        // POST: /Requests/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Request request)
        {
            if (id != request.RequestID)
                return NotFound();

            request.Tenant = null;
            ValidateRequest(request);

            if (request.TenantID > 0 &&
                !await _context.Tenants.AnyAsync(t => t.TenantID == request.TenantID))
            {
                ModelState.AddModelError(nameof(request.TenantID), "Người thuê được chọn không tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                await LoadOptionsAsync(request.TenantID, request.Status);
                return View(request);
            }

            var existingRequest = await _context.Requests
                .FirstOrDefaultAsync(r => r.RequestID == id);

            if (existingRequest == null)
                return NotFound();

            existingRequest.TenantID = request.TenantID;
            existingRequest.Content = request.Content.Trim();
            existingRequest.CreatedDate = request.CreatedDate == default
                ? existingRequest.CreatedDate
                : request.CreatedDate;
            existingRequest.Status = string.IsNullOrWhiteSpace(request.Status)
                ? "Pending"
                : request.Status.Trim();
            existingRequest.Response = string.IsNullOrWhiteSpace(request.Response)
                ? null
                : request.Response.Trim();

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Requests/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await _context.Requests
                .AsNoTracking()
                .Include(r => r.Tenant)
                .FirstOrDefaultAsync(r => r.RequestID == id.Value);

            if (request == null)
                return NotFound();

            return View(request);
        }

        // POST: /Requests/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var request = await _context.Requests.FindAsync(id);
            if (request == null)
                return NotFound();

            _context.Requests.Remove(request);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private void ValidateRequest(Request request)
        {
            if (request.TenantID <= 0)
                ModelState.AddModelError(nameof(request.TenantID), "Vui lòng chọn người thuê.");

            if (string.IsNullOrWhiteSpace(request.Content))
                ModelState.AddModelError(nameof(request.Content), "Nội dung yêu cầu không được để trống.");

            if (request.Content?.Length > 2000)
                ModelState.AddModelError(nameof(request.Content), "Nội dung không được vượt quá 2000 ký tự.");

            if (request.CreatedDate > DateTime.Now.AddMinutes(5))
                ModelState.AddModelError(nameof(request.CreatedDate), "Ngày tạo không được nằm trong tương lai.");

            if (request.Response?.Length > 2000)
                ModelState.AddModelError(nameof(request.Response), "Phản hồi không được vượt quá 2000 ký tự.");

            if (string.IsNullOrWhiteSpace(request.Status))
                request.Status = "Pending";
        }

        private async Task LoadOptionsAsync(int? selectedTenantId, string? selectedStatus)
        {
            var tenantIds = await _context.Tenants
                .AsNoTracking()
                .OrderBy(t => t.TenantID)
                .Select(t => t.TenantID)
                .ToListAsync();

            ViewBag.TenantOptions = tenantIds.Select(tenantId => new SelectListItem
            {
                Value = tenantId.ToString(),
                Text = $"Người thuê #{tenantId}",
                Selected = selectedTenantId == tenantId
            }).ToList();

            var statuses = DefaultStatuses.ToList();
            if (!string.IsNullOrWhiteSpace(selectedStatus) &&
                !statuses.Contains(selectedStatus, StringComparer.OrdinalIgnoreCase))
            {
                statuses.Add(selectedStatus);
            }

            ViewBag.StatusOptions = statuses.Select(status => new SelectListItem
            {
                Value = status,
                Text = status,
                Selected = string.Equals(status, selectedStatus, StringComparison.OrdinalIgnoreCase)
            }).ToList();
        }
    }
}
