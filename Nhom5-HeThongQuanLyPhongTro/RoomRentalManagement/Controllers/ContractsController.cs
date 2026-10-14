using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Data;
using RoomRentalManagement.Models;

namespace RoomRentalManagement.Controllers
{
    public class ContractsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContractsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Contracts
        public async Task<IActionResult> Index()
        {
            var contracts = await _context.Contracts
                .AsNoTracking()
                .Include(c => c.Room)
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            return View(contracts);
        }

        // GET: Contracts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var contract = await _context.Contracts
                .AsNoTracking()
                .Include(c => c.Room)
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.ContractID == id);

            if (contract == null) return NotFound();
            return View(contract);
        }

        // GET: Contracts/Create
        public async Task<IActionResult> Create()
        {
            await LoadDropdownsAsync();
            return View(new Contract { StartDate = DateTime.Today, Status = "Active" });
        }

        // POST: Contracts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("RoomID,TenantID,StartDate,EndDate,Deposit,RentPrice,Status")] Contract contract)
        {
            await ValidateContractAsync(contract, null);

            if (ModelState.IsValid)
            {
                contract.Status = NormalizeStatus(contract.Status);
                contract.Room = null;
                contract.Tenant = null;

                if (IsActive(contract.Status))
                {
                    var room = await _context.Rooms.FindAsync(contract.RoomID);
                    if (room == null) return NotFound();
                    room.Status = "đang thuê";
                }

                _context.Contracts.Add(contract);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đã tạo hợp đồng thành công.";
                return RedirectToAction(nameof(Index));
            }

            await LoadDropdownsAsync(contract.RoomID, contract.TenantID);
            return View(contract);
        }

        // GET: Contracts/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var contract = await _context.Contracts.FindAsync(id);
            if (contract == null) return NotFound();

            await LoadDropdownsAsync(contract.RoomID, contract.TenantID);
            return View(contract);
        }

        // POST: Contracts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("ContractID,RoomID,TenantID,StartDate,EndDate,Deposit,RentPrice,Status")] Contract input)
        {
            if (id != input.ContractID) return NotFound();

            await ValidateContractAsync(input, id);
            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync(input.RoomID, input.TenantID);
                return View(input);
            }

            var contract = await _context.Contracts
                .FirstOrDefaultAsync(c => c.ContractID == id);
            if (contract == null) return NotFound();

            var oldRoomId = contract.RoomID;

            contract.RoomID = input.RoomID;
            contract.TenantID = input.TenantID;
            contract.StartDate = input.StartDate;
            contract.EndDate = input.EndDate;
            contract.Deposit = input.Deposit;
            contract.RentPrice = input.RentPrice;
            contract.Status = NormalizeStatus(input.Status);

            // Nếu hợp đồng cũ chuyển trạng thái/khu vực, giải phóng phòng cũ
            // khi không còn hợp đồng Active nào khác gắn với phòng đó.
            if (oldRoomId != contract.RoomID || !IsActive(contract.Status))
            {
                var otherActiveOnOldRoom = await _context.Contracts.AnyAsync(c =>
                    c.ContractID != id && c.RoomID == oldRoomId && c.Status == "Active");

                if (!otherActiveOnOldRoom)
                {
                    var oldRoom = await _context.Rooms.FindAsync(oldRoomId);
                    if (oldRoom != null) oldRoom.Status = "trống";
                }
            }

            // Hợp đồng Active làm phòng được đánh dấu là đang thuê.
            if (IsActive(contract.Status))
            {
                var newRoom = await _context.Rooms.FindAsync(contract.RoomID);
                if (newRoom == null) return NotFound();
                newRoom.Status = "đang thuê";
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật hợp đồng.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Contracts/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var contract = await _context.Contracts
                .AsNoTracking()
                .Include(c => c.Room)
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.ContractID == id);

            if (contract == null) return NotFound();

            ViewBag.CanDelete = !await HasInvoicesAsync(id.Value);
            return View(contract);
        }

        // POST: Contracts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var contract = await _context.Contracts.FindAsync(id);
            if (contract == null) return NotFound();

            // Không xóa hợp đồng đã phát sinh hóa đơn vì database đặt khóa ngoại Restrict.
            if (await HasInvoicesAsync(id))
            {
                TempData["Error"] = "Không thể xóa hợp đồng này vì đã có hóa đơn liên quan.";
                return RedirectToAction(nameof(Index));
            }

            var roomId = contract.RoomID;
            var wasActive = IsActive(contract.Status);
            _context.Contracts.Remove(contract);

            if (wasActive)
            {
                var otherActiveOnRoom = await _context.Contracts.AnyAsync(c =>
                    c.ContractID != id && c.RoomID == roomId && c.Status == "Active");
                if (!otherActiveOnRoom)
                {
                    var room = await _context.Rooms.FindAsync(roomId);
                    if (room != null) room.Status = "trống";
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xóa hợp đồng.";
            return RedirectToAction(nameof(Index));
        }

        private async Task ValidateContractAsync(Contract contract, int? currentContractId)
        {
            if (contract.RoomID <= 0 || !await _context.Rooms.AnyAsync(r => r.RoomID == contract.RoomID))
                ModelState.AddModelError(nameof(contract.RoomID), "Vui lòng chọn phòng hợp lệ.");

            if (contract.TenantID <= 0 || !await _context.Tenants.AnyAsync(t => t.TenantID == contract.TenantID))
                ModelState.AddModelError(nameof(contract.TenantID), "Vui lòng chọn người thuê hợp lệ.");

            if (contract.StartDate == default)
                ModelState.AddModelError(nameof(contract.StartDate), "Vui lòng nhập ngày bắt đầu.");

            if (contract.EndDate.HasValue && contract.StartDate != default && contract.EndDate.Value.Date < contract.StartDate.Date)
                ModelState.AddModelError(nameof(contract.EndDate), "Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.");

            if (contract.Deposit < 0)
                ModelState.AddModelError(nameof(contract.Deposit), "Tiền đặt cọc không được âm.");

            if (contract.RentPrice <= 0)
                ModelState.AddModelError(nameof(contract.RentPrice), "Giá thuê phải lớn hơn 0.");

            var allowedStatuses = new[] { "Active", "Expired", "Terminated" };
            if (string.IsNullOrWhiteSpace(contract.Status) || !allowedStatuses.Contains(contract.Status))
                ModelState.AddModelError(nameof(contract.Status), "Vui lòng chọn trạng thái hợp đồng hợp lệ.");

            // Mỗi phòng chỉ được có một hợp đồng Active tại một thời điểm.
            if (contract.Status == "Active" && contract.RoomID > 0)
            {
                var roomHasOtherActiveContract = await _context.Contracts.AnyAsync(c =>
                    c.RoomID == contract.RoomID && c.Status == "Active" &&
                    (!currentContractId.HasValue || c.ContractID != currentContractId.Value));

                if (roomHasOtherActiveContract)
                    ModelState.AddModelError(nameof(contract.RoomID), "Phòng này đã có hợp đồng đang hiệu lực. Hãy chọn phòng khác hoặc cập nhật hợp đồng hiện tại.");
            }
        }

        private async Task LoadDropdownsAsync(int? selectedRoomId = null, int? selectedTenantId = null)
        {
            var rooms = await _context.Rooms
                .AsNoTracking()
                .OrderBy(r => r.RoomName)
                .ToListAsync();

            var tenants = await _context.Tenants
                .AsNoTracking()
                .OrderBy(t => t.FullName)
                .ToListAsync();

            ViewBag.RoomID = new SelectList(rooms, "RoomID", "RoomName", selectedRoomId);
            ViewBag.TenantID = new SelectList(tenants, "TenantID", "FullName", selectedTenantId);
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
