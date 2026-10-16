using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Data;
using RoomRentalManagement.Models;

namespace RoomRentalManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RoomsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RoomsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Rooms
        public async Task<IActionResult> Index()
        {
            var rooms = await _context.Rooms
                .AsNoTracking()
                .OrderBy(r => r.RoomName)
                .ToListAsync();

            return View(rooms);
        }

        // GET: /Rooms/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var room = await _context.Rooms
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoomID == id.Value);

            if (room == null) return NotFound();
            return View(room);
        }

        // GET: /Rooms/Create
        public IActionResult Create() => View();

        // POST: /Rooms/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("RoomName,Price,Area,Status,Description")] Room room)
        {
            ValidateRoom(room);
            if (!ModelState.IsValid) return View(room);

            room.RoomName = room.RoomName.Trim();
            room.Status = string.IsNullOrWhiteSpace(room.Status) ? "Available" : room.Status.Trim();
            room.Description = string.IsNullOrWhiteSpace(room.Description) ? null : room.Description.Trim();

            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thêm phòng trọ thành công.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Rooms/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var room = await _context.Rooms.FindAsync(id.Value);
            if (room == null) return NotFound();
            return View(room);
        }

        // POST: /Rooms/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("RoomID,RoomName,Price,Area,Status,Description")] Room input)
        {
            if (id != input.RoomID) return NotFound();

            ValidateRoom(input);
            if (!ModelState.IsValid) return View(input);

            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();

            room.RoomName = input.RoomName.Trim();
            room.Price = input.Price;
            room.Area = input.Area;
            room.Status = string.IsNullOrWhiteSpace(input.Status) ? "Available" : input.Status.Trim();
            room.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cập nhật phòng trọ thành công.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Rooms/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var room = await _context.Rooms
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoomID == id.Value);

            if (room == null) return NotFound();
            return View(room);
        }

        // POST: /Rooms/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();

            // Không xóa phòng đang được hợp đồng tham chiếu.
            if (await _context.Contracts.AnyAsync(c => c.RoomID == id))
            {
                TempData["Error"] = "Không thể xóa phòng vì đã có hợp đồng liên quan.";
                return RedirectToAction(nameof(Index));
            }

            _context.Rooms.Remove(room);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Xóa phòng trọ thành công.";
            return RedirectToAction(nameof(Index));
        }

        private void ValidateRoom(Room room)
        {
            if (string.IsNullOrWhiteSpace(room.RoomName))
                ModelState.AddModelError(nameof(room.RoomName), "Vui lòng nhập tên phòng.");
            if (room.Price <= 0)
                ModelState.AddModelError(nameof(room.Price), "Giá phòng phải lớn hơn 0.");
            if (room.Area <= 0)
                ModelState.AddModelError(nameof(room.Area), "Diện tích phải lớn hơn 0.");
            var allowedStatuses = new[] { "Available", "Occupied", "Maintenance", "trống", "đang thuê", "bảo trì" };
            if (string.IsNullOrWhiteSpace(room.Status) || !allowedStatuses.Contains(room.Status.Trim(), StringComparer.OrdinalIgnoreCase))
                ModelState.AddModelError(nameof(room.Status), "Trạng thái phòng không hợp lệ.");
        }
    }
}
