
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
    public class ContractsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContractsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int CurrentUserID =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Hợp đồng mà tài khoản hiện tại được phép xem
        private IQueryable<Contract> AccessibleContracts()
        {
            var query = _context.Contracts.AsQueryable();

            if (User.IsInRole("Admin"))
                return query;

            if (User.IsInRole("Landlord"))
            {
                return query.Where(c =>
                    c.Room != null && c.Room.LandlordID == CurrentUserID);
            }

            if (User.IsInRole("Tenant"))
            {
                return query.Where(c =>
                    c.Tenant != null &&
                    c.Tenant.UserID == CurrentUserID);
            }

            return query.Where(c => false);
        }

        public async Task<IActionResult> Index()
        {
            var contracts = await AccessibleContracts()
                .AsNoTracking()
                .Include(c => c.Room)
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            return View(contracts);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var contract = await AccessibleContracts()
                .AsNoTracking()
                .Include(c => c.Room)
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.ContractID == id);

            if (contract == null)
                return NotFound();

            return View(contract);
        }

        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Create()
        {
            await LoadDropdownsAsync();

            return View(new Contract
            {
                StartDate = DateTime.Today,
                Status = "Active"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Create(
            [Bind("RoomID,TenantID,StartDate,EndDate,Deposit,RentPrice,Status")]
            Contract contract)
        {
            var room = await AccessibleRooms()
                .FirstOrDefaultAsync(r => r.RoomID == contract.RoomID);

            if (room == null)
            {
                ModelState.AddModelError(
                    nameof(contract.RoomID),
                    "Phòng không tồn tại hoặc không thuộc quyền quản lý của bạn.");
            }

            if (!await _context.Tenants.AnyAsync(
                t => t.TenantID == contract.TenantID))
            {
                ModelState.AddModelError(
                    nameof(contract.TenantID),
                    "Vui lòng chọn người thuê hợp lệ.");
            }

            await ValidateContractAsync(contract, null);

            if (ModelState.IsValid && room != null)
            {
                contract.Status = NormalizeStatus(contract.Status);

                if (IsActive(contract.Status))
                    room.Status = "đang thuê";

                _context.Contracts.Add(contract);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đã tạo hợp đồng thành công.";
                return RedirectToAction(nameof(Index));
            }

            await LoadDropdownsAsync(contract.RoomID, contract.TenantID);
            return View(contract);
        }

        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var contract = await AccessibleContracts()
                .FirstOrDefaultAsync(c => c.ContractID == id);

            if (contract == null)
                return NotFound();

            await LoadDropdownsAsync(contract.RoomID, contract.TenantID);
            return View(contract);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("ContractID,RoomID,TenantID,StartDate,EndDate,Deposit,RentPrice,Status")]
            Contract input)
        {
            if (id != input.ContractID)
                return NotFound();

            var contract = await AccessibleContracts()
                .FirstOrDefaultAsync(c => c.ContractID == id);

            if (contract == null)
                return NotFound();

            var newRoom = await AccessibleRooms()
                .FirstOrDefaultAsync(r => r.RoomID == input.RoomID);

            if (newRoom == null)
            {
                ModelState.AddModelError(
                    nameof(input.RoomID),
                    "Phòng không tồn tại hoặc không thuộc quyền quản lý của bạn.");
            }

            await ValidateContractAsync(input, id);

            if (!ModelState.IsValid || newRoom == null)
            {
                await LoadDropdownsAsync(input.RoomID, input.TenantID);
                return View(input);
            }

            var oldRoomId = contract.RoomID;

            contract.RoomID = input.RoomID;
            contract.TenantID = input.TenantID;
            contract.StartDate = input.StartDate;
            contract.EndDate = input.EndDate;
            contract.Deposit = input.Deposit;
            contract.RentPrice = input.RentPrice;
            contract.Status = NormalizeStatus(input.Status);

            if (oldRoomId != contract.RoomID || !IsActive(contract.Status))
            {
                var otherActive = await _context.Contracts.AnyAsync(c =>
                    c.ContractID != id &&
                    c.RoomID == oldRoomId &&
                    c.Status == "Active");

                if (!otherActive)
                {
                    var oldRoom = await _context.Rooms.FindAsync(oldRoomId);
                    if (oldRoom != null)
                        oldRoom.Status = "trống";
                }
            }

            if (IsActive(contract.Status))
                newRoom.Status = "đang thuê";

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật hợp đồng.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var contract = await AccessibleContracts()
                .AsNoTracking()
                .Include(c => c.Room)
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.ContractID == id);

            if (contract == null)
                return NotFound();

            ViewBag.CanDelete = !await HasInvoicesAsync(id.Value);
            return View(contract);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var contract = await AccessibleContracts()
                .FirstOrDefaultAsync(c => c.ContractID == id);

            if (contract == null)
                return NotFound();

            if (await HasInvoicesAsync(id))
            {
                TempData["Error"] =
                    "Không thể xóa hợp đồng này vì đã có hóa đơn liên quan.";
                return RedirectToAction(nameof(Index));
            }

            var roomId = contract.RoomID;
            var wasActive = IsActive(contract.Status);

            _context.Contracts.Remove(contract);

            if (wasActive)
            {
                var otherActive = await _context.Contracts.AnyAsync(c =>
                    c.ContractID != id &&
                    c.RoomID == roomId &&
                    c.Status == "Active");

                if (!otherActive)
                {
                    var room = await _context.Rooms.FindAsync(roomId);
                    if (room != null)
                        room.Status = "trống";
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa hợp đồng.";
            return RedirectToAction(nameof(Index));
        }

        private IQueryable<Room> AccessibleRooms()
        {
            var rooms = _context.Rooms.AsQueryable();

            if (User.IsInRole("Admin"))
                return rooms;

            if (User.IsInRole("Landlord"))
                return rooms.Where(r => r.LandlordID == CurrentUserID);

            return rooms.Where(r => false);
        }

        private async Task ValidateContractAsync(
            Contract contract,
            int? currentContractId)
        {
            if (contract.RoomID <= 0 ||
                !await AccessibleRooms().AnyAsync(r => r.RoomID == contract.RoomID))
            {
                ModelState.AddModelError(
                    nameof(contract.RoomID),
                    "Vui lòng chọn phòng hợp lệ thuộc quyền quản lý.");
            }

            if (contract.TenantID <= 0 ||
                !await _context.Tenants.AnyAsync(t => t.TenantID == contract.TenantID))
            {
                ModelState.AddModelError(
                    nameof(contract.TenantID),
                    "Vui lòng chọn người thuê hợp lệ.");
            }

            if (contract.StartDate == default)
                ModelState.AddModelError(
                    nameof(contract.StartDate), "Vui lòng nhập ngày bắt đầu.");

            if (contract.EndDate.HasValue &&
                contract.StartDate != default &&
                contract.EndDate.Value.Date < contract.StartDate.Date)
            {
                ModelState.AddModelError(
                    nameof(contract.EndDate),
                    "Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.");
            }

            if (contract.Deposit < 0)
                ModelState.AddModelError(
                    nameof(contract.Deposit), "Tiền đặt cọc không được âm.");

            if (contract.RentPrice <= 0)
                ModelState.AddModelError(
                    nameof(contract.RentPrice), "Giá thuê phải lớn hơn 0.");

            var allowedStatuses = new[] { "Active", "Expired", "Terminated" };

            if (string.IsNullOrWhiteSpace(contract.Status) ||
                !allowedStatuses.Contains(contract.Status))
            {
                ModelState.AddModelError(
                    nameof(contract.Status), "Vui lòng chọn trạng thái hợp đồng hợp lệ.");
            }

            if (contract.Status == "Active" && contract.RoomID > 0)
            {
                var roomHasOtherActive = await _context.Contracts.AnyAsync(c =>
                    c.RoomID == contract.RoomID &&
                    c.Status == "Active" &&
                    (!currentContractId.HasValue ||
                     c.ContractID != currentContractId.Value));

                if (roomHasOtherActive)
                {
                    ModelState.AddModelError(
                        nameof(contract.RoomID),
                        "Phòng này đã có hợp đồng đang hiệu lực.");
                }
            }
        }

        private async Task LoadDropdownsAsync(
            int? selectedRoomId = null,
            int? selectedTenantId = null)
        {
            var rooms = await AccessibleRooms()
                .AsNoTracking()
                .OrderBy(r => r.RoomName)
                .ToListAsync();

            var tenants = await _context.Tenants
                .AsNoTracking()
                .OrderBy(t => t.FullName)
                .ToListAsync();

            ViewBag.RoomID = new SelectList(
                rooms, "RoomID", "RoomName", selectedRoomId);

            ViewBag.TenantID = new SelectList(
                tenants, "TenantID", "FullName", selectedTenantId);
        }

        private Task<bool> HasInvoicesAsync(int contractId)
        {
            return _context.Invoices.AnyAsync(i => i.ContractID == contractId);
        }

        private static bool IsActive(string? status) =>
            string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);

        private static string NormalizeStatus(string? status) =>
            string.IsNullOrWhiteSpace(status) ? "Active" : status.Trim();
    }
}
