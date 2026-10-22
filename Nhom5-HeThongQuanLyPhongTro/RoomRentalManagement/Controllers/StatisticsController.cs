using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Data;

namespace RoomRentalManagement.Controllers
{
    [Authorize(Roles = "Admin,Landlord,Tenant")]
    public class StatisticsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StatisticsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int CurrentUserID =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public async Task<IActionResult> Index()
        {
            // ADMIN: Xem thống kê toàn hệ thống
            if (User.IsInRole("Admin"))
            {
                ViewBag.TotalRooms = await _context.Rooms.CountAsync();
                ViewBag.TotalTenants = await _context.Tenants.CountAsync();
                ViewBag.TotalContracts = await _context.Contracts.CountAsync();
                ViewBag.TotalInvoices = await _context.Invoices.CountAsync();
                ViewBag.TotalPayments = await _context.Payments.CountAsync();
                ViewBag.TotalRequests = await _context.Requests.CountAsync();

                ViewBag.TotalRevenue =
                    await _context.Payments.SumAsync(
                        p => (decimal?)p.Amount) ?? 0m;

                return View();
            }

            // CHỦ NHÀ: Chỉ xem dữ liệu thuộc phòng mình quản lý
            if (User.IsInRole("Landlord"))
            {
                var rooms = _context.Rooms
                    .Where(r => r.LandlordID == CurrentUserID);

                var contracts = _context.Contracts
                    .Where(c => c.Room != null &&
                                c.Room.LandlordID == CurrentUserID);

                var invoices = _context.Invoices
                    .Where(i => i.Contract != null &&
                                i.Contract.Room != null &&
                                i.Contract.Room.LandlordID == CurrentUserID);

                var tenants = _context.Tenants
                    .Where(t => contracts.Any(c =>
                        c.TenantID == t.TenantID));

                var requests = _context.Requests
                    .Where(r => contracts.Any(c =>
                        c.TenantID == r.TenantID));

                ViewBag.TotalRooms = await rooms.CountAsync();
                ViewBag.TotalTenants = await tenants.Distinct().CountAsync();
                ViewBag.TotalContracts = await contracts.CountAsync();
                ViewBag.TotalInvoices = await invoices.CountAsync();

                ViewBag.TotalPayments = await _context.Payments
                    .CountAsync(p => invoices.Any(i =>
                        i.InvoiceID == p.InvoiceID));

                ViewBag.TotalRequests = await requests.CountAsync();

                ViewBag.TotalRevenue = await _context.Payments
                    .Where(p => invoices.Any(i =>
                        i.InvoiceID == p.InvoiceID))
                    .SumAsync(p => (decimal?)p.Amount) ?? 0m;

                return View();
            }

            // NGƯỜI THUÊ: Chỉ xem dữ liệu của tài khoản mình
            var myTenants = _context.Tenants
                .Where(t => t.UserID == CurrentUserID);

            var myContracts = _context.Contracts
                .Where(c => myTenants.Any(t =>
                    t.TenantID == c.TenantID));

            var myInvoices = _context.Invoices
                .Where(i => myContracts.Any(c =>
                    c.ContractID == i.ContractID));

            ViewBag.TotalRooms = await _context.Rooms
                .CountAsync(r => myContracts.Any(c =>
                    c.RoomID == r.RoomID));

            ViewBag.TotalTenants = await myTenants.CountAsync();
            ViewBag.TotalContracts = await myContracts.CountAsync();
            ViewBag.TotalInvoices = await myInvoices.CountAsync();

            ViewBag.TotalPayments = await _context.Payments
                .CountAsync(p => myInvoices.Any(i =>
                    i.InvoiceID == p.InvoiceID));

            ViewBag.TotalRequests = await _context.Requests
                .CountAsync(r => myTenants.Any(t =>
                    t.TenantID == r.TenantID));

            ViewBag.TotalRevenue = await _context.Payments
                .Where(p => myInvoices.Any(i =>
                    i.InvoiceID == p.InvoiceID))
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            return View();
        }
    }
}
