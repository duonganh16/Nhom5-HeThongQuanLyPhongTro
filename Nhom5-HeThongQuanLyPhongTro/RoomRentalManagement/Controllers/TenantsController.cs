using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Data;
using RoomRentalManagement.Models;

namespace RoomRentalManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class TenantsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TenantsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Tenants
        public async Task<IActionResult> Index()
        {
            var tenants = await _context.Tenants
                .AsNoTracking()
                .OrderBy(t => t.FullName)
                .ToListAsync();

            return View(tenants);
        }

        // GET: Tenants/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TenantID == id);

            if (tenant == null) return NotFound();
            return View(tenant);
        }

        // GET: Tenants/Create
        public IActionResult Create() => View();

        // POST: Tenants/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("FullName,Phone,CCCD,DateOfBirth,Address")] Tenant tenant)
        {
            ValidateTenant(tenant);

            if (!string.IsNullOrWhiteSpace(tenant.CCCD) &&
                await _context.Tenants.AnyAsync(t => t.CCCD == tenant.CCCD.Trim()))
            {
                ModelState.AddModelError(nameof(tenant.CCCD), "Số CCCD này đã tồn tại.");
            }

            if (!ModelState.IsValid) return View(tenant);

            tenant.FullName = tenant.FullName.Trim();
            tenant.Phone = tenant.Phone.Trim();
            tenant.CCCD = tenant.CCCD.Trim();
            tenant.Address = string.IsNullOrWhiteSpace(tenant.Address) ? null : tenant.Address.Trim();

            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã thêm người thuê thành công.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Tenants/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();

            return View(tenant);
        }

        // POST: Tenants/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("TenantID,FullName,Phone,CCCD,DateOfBirth,Address")] Tenant input)
        {
            if (id != input.TenantID) return NotFound();

            ValidateTenant(input);

            if (!string.IsNullOrWhiteSpace(input.CCCD) &&
                await _context.Tenants.AnyAsync(t => t.CCCD == input.CCCD.Trim() && t.TenantID != id))
            {
                ModelState.AddModelError(nameof(input.CCCD), "Số CCCD này đã được người thuê khác sử dụng.");
            }

            if (!ModelState.IsValid) return View(input);

            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();

            // Chỉ cập nhật các trường người dùng được phép sửa; giữ nguyên UserID.
            tenant.FullName = input.FullName.Trim();
            tenant.Phone = input.Phone.Trim();
            tenant.CCCD = input.CCCD.Trim();
            tenant.DateOfBirth = input.DateOfBirth;
            tenant.Address = string.IsNullOrWhiteSpace(input.Address) ? null : input.Address.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật thông tin người thuê.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Tenants/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TenantID == id);

            if (tenant == null) return NotFound();

            ViewBag.CanDelete = !await HasRelatedRecords(id.Value);
            return View(tenant);
        }

        // POST: Tenants/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant == null) return NotFound();

            // Hợp đồng/yêu cầu đang tham chiếu người thuê thì không cho xóa.
            if (await HasRelatedRecords(id))
            {
                TempData["Error"] = "Không thể xóa người thuê này vì đã có hợp đồng hoặc yêu cầu liên quan.";
                return RedirectToAction(nameof(Index));
            }

            _context.Tenants.Remove(tenant);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa người thuê.";
            return RedirectToAction(nameof(Index));
        }

        private void ValidateTenant(Tenant tenant)
        {
            if (string.IsNullOrWhiteSpace(tenant.FullName))
                ModelState.AddModelError(nameof(tenant.FullName), "Vui lòng nhập họ tên.");

            if (string.IsNullOrWhiteSpace(tenant.Phone))
                ModelState.AddModelError(nameof(tenant.Phone), "Vui lòng nhập số điện thoại.");

            if (string.IsNullOrWhiteSpace(tenant.CCCD))
                ModelState.AddModelError(nameof(tenant.CCCD), "Vui lòng nhập số CCCD.");

            if (tenant.DateOfBirth.HasValue && tenant.DateOfBirth.Value.Date > DateTime.Today)
                ModelState.AddModelError(nameof(tenant.DateOfBirth), "Ngày sinh không được ở tương lai.");
        }

        private async Task<bool> HasRelatedRecords(int tenantId)
        {
            var hasContract = await _context.Contracts.AnyAsync(c => c.TenantID == tenantId);
            var hasRequest = await _context.Requests.AnyAsync(r => r.TenantID == tenantId);
            return hasContract || hasRequest;
        }
    }
}
