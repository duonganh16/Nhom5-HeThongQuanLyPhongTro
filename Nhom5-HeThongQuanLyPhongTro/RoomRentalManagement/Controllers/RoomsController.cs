
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Data;
using RoomRentalManagement.Models;
using System.Security.Claims;

[Authorize(Roles = "Admin,Landlord")]
public class RoomsController : Controller
{
    private readonly ApplicationDbContext _context;

    public RoomsController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int CurrentUserID =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private IQueryable<Room> AccessibleRooms()
    {
        if (User.IsInRole("Admin"))
            return _context.Rooms;

        return _context.Rooms.Where(r => r.LandlordID == CurrentUserID);
    }

    public async Task<IActionResult> Index()
    {
        return View(await AccessibleRooms().ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var room = await AccessibleRooms()
            .FirstOrDefaultAsync(r => r.RoomID == id);

        if (room == null) return NotFound();

        return View(room);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("RoomName,Price,Area,Status,Description")] Room room)
    {
        if (!ModelState.IsValid)
            return View(room);

        if (User.IsInRole("Landlord"))
            room.LandlordID = CurrentUserID;
        else
            room.LandlordID = null;

        _context.Rooms.Add(room);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var room = await AccessibleRooms()
            .FirstOrDefaultAsync(r => r.RoomID == id);

        if (room == null) return NotFound();

        return View(room);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int? id,
        [Bind("RoomID,RoomName,Price,Area,Status,Description")] Room input)
    {
        if (id != input.RoomID) return NotFound();

        var room = await AccessibleRooms()
            .FirstOrDefaultAsync(r => r.RoomID == id);

        if (room == null) return NotFound();

        if (!ModelState.IsValid)
            return View(input);

        room.RoomName = input.RoomName;
        room.Price = input.Price;
        room.Area = input.Area;
        room.Status = input.Status;
        room.Description = input.Description;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var room = await AccessibleRooms()
            .FirstOrDefaultAsync(r => r.RoomID == id);

        if (room == null) return NotFound();

        return View(room);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var room = await AccessibleRooms()
            .FirstOrDefaultAsync(r => r.RoomID == id);

        if (room == null) return NotFound();

        _context.Rooms.Remove(room);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
