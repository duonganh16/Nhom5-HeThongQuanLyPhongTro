
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Data;

namespace RoomRentalManagement.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var isAdmin = User.IsInRole("Admin");
            var isLandlord = User.IsInRole("Landlord");
            var isTenant = User.IsInRole("Tenant");

            var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdText, out var userId);

            var roomsQuery = _context.Rooms.AsNoTracking();
            var contractsQuery = _context.Contracts
                .AsNoTracking()
                .Include(c => c.Room)
                .Include(c => c.Tenant)
                .AsQueryable();

            var invoicesQuery = _context.Invoices
                .AsNoTracking()
                .Include(i => i.Contract)
                    .ThenInclude(c => c!.Room)
                .Include(i => i.Contract)
                    .ThenInclude(c => c!.Tenant)
                .AsQueryable();

            var paymentsQuery = _context.Payments
                .AsNoTracking()
                .Include(p => p.Invoice)
                    .ThenInclude(i => i!.Contract)
                        .ThenInclude(c => c!.Room)
                .AsQueryable();

            var requestsQuery = _context.Requests.AsNoTracking();

            int? tenantId = null;

            if (isLandlord)
            {
                roomsQuery = roomsQuery.Where(r => r.LandlordID == userId);

                contractsQuery = contractsQuery.Where(
                    c => c.Room != null && c.Room.LandlordID == userId);

                invoicesQuery = invoicesQuery.Where(
                    i => i.Contract != null &&
                         i.Contract.Room != null &&
                         i.Contract.Room.LandlordID == userId);

                paymentsQuery = paymentsQuery.Where(
                    p => p.Invoice != null &&
                         p.Invoice.Contract != null &&
                         p.Invoice.Contract.Room != null &&
                         p.Invoice.Contract.Room.LandlordID == userId);

                requestsQuery = requestsQuery.Where(
                    r => _context.Contracts.Any(c =>
                        c.TenantID == r.TenantID &&
                        c.Room != null &&
                        c.Room.LandlordID == userId));
            }
            else if (isTenant)
            {
                tenantId = await _context.Tenants
                    .Where(t => t.UserID == userId)
                    .Select(t => (int?)t.TenantID)
                    .FirstOrDefaultAsync();

                roomsQuery = roomsQuery.Where(r =>
                    _context.Contracts.Any(c =>
                        c.RoomID == r.RoomID &&
                        c.TenantID == tenantId));

                contractsQuery = contractsQuery.Where(
                    c => tenantId.HasValue && c.TenantID == tenantId.Value);

                invoicesQuery = invoicesQuery.Where(
                    i => tenantId.HasValue &&
                         i.Contract != null &&
                         i.Contract.TenantID == tenantId.Value);

                paymentsQuery = paymentsQuery.Where(
                    p => tenantId.HasValue &&
                         p.Invoice != null &&
                         p.Invoice.Contract != null &&
                         p.Invoice.Contract.TenantID == tenantId.Value);

                requestsQuery = requestsQuery.Where(
                    r => tenantId.HasValue && r.TenantID == tenantId.Value);
            }

            var rooms = await roomsQuery.ToListAsync();
            var contracts = await contractsQuery.ToListAsync();
            var invoices = await invoicesQuery.ToListAsync();

            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var nextMonth = monthStart.AddMonths(1);

            var revenueThisMonth = await paymentsQuery
                .Where(p => p.PaymentDate >= monthStart &&
                            p.PaymentDate < nextMonth)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            ViewBag.TotalRooms = rooms.Count;

            ViewBag.OccupiedRooms = rooms.Count(r =>
                (r.Status ?? "").Contains("thuê",
                    StringComparison.OrdinalIgnoreCase));

            ViewBag.VacantRooms = rooms.Count - (int)ViewBag.OccupiedRooms;

            ViewBag.TotalContracts = contracts.Count;

            ViewBag.TotalTenants = isTenant
                ? (tenantId.HasValue ? 1 : 0)
                : isLandlord
                    ? contracts.Select(c => c.TenantID).Distinct().Count()
                    : await _context.Tenants.CountAsync();

            ViewBag.TotalInvoices = invoices.Count;

            ViewBag.UnpaidInvoices = invoices.Count(i =>
                (i.Status ?? "").Equals("Unpaid",
                    StringComparison.OrdinalIgnoreCase) ||
                (i.Status ?? "").Equals("Overdue",
                    StringComparison.OrdinalIgnoreCase));

            ViewBag.RevenueThisMonth = revenueThisMonth;

            ViewBag.TotalRequests = await requestsQuery.CountAsync();

            ViewBag.RecentInvoices = invoices
                .OrderByDescending(i => i.BillingMonth)
                .ThenByDescending(i => i.InvoiceID)
                .Take(5)
                .ToList();

            ViewBag.RecentPayments = await paymentsQuery
                .OrderByDescending(p => p.PaymentDate)
                .ThenByDescending(p => p.PaymentID)
                .Take(5)
                .ToListAsync();

            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsLandlord = isLandlord;
            ViewBag.IsTenant = isTenant;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
