
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Data;
using RoomRentalManagement.Models;

namespace RoomRentalManagement.Controllers
{
    [Authorize(Roles = "Admin,Landlord,Tenant")]
    public class InvoicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        private static readonly string[] AllowedStatuses =
        {
            "Unpaid", "Paid", "Overdue"
        };

        public InvoicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int CurrentUserID =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Chỉ lấy hóa đơn mà tài khoản hiện tại được phép xem.
        private IQueryable<Invoice> AccessibleInvoices()
        {
            var query = _context.Invoices.AsQueryable();

            if (User.IsInRole("Admin"))
                return query;

            if (User.IsInRole("Landlord"))
            {
                return query.Where(i =>
                    i.Contract != null &&
                    i.Contract.Room != null &&
                    i.Contract.Room.LandlordID == CurrentUserID);
            }

            if (User.IsInRole("Tenant"))
            {
                return query.Where(i =>
                    i.Contract != null &&
                    i.Contract.Tenant != null &&
                    i.Contract.Tenant.UserID == CurrentUserID);
            }

            return query.Where(i => false);
        }

        // Hợp đồng được phép dùng khi tạo hoặc sửa hóa đơn.
        private IQueryable<Contract> AccessibleContracts()
        {
            var query = _context.Contracts.AsQueryable();

            if (User.IsInRole("Admin"))
                return query;

            if (User.IsInRole("Landlord"))
            {
                return query.Where(c =>
                    c.Room != null &&
                    c.Room.LandlordID == CurrentUserID);
            }

            return query.Where(c => false);
        }

        public async Task<IActionResult> Index()
        {
            var invoices = await AccessibleInvoices()
                .AsNoTracking()
                .Include(i => i.Contract)
                    .ThenInclude(c => c!.Room)
                .Include(i => i.Contract)
                    .ThenInclude(c => c!.Tenant)
                .OrderByDescending(i => i.BillingMonth)
                .ThenByDescending(i => i.InvoiceID)
                .ToListAsync();

            return View(invoices);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var invoice = await AccessibleInvoices()
                .AsNoTracking()
                .Include(i => i.Contract)
                    .ThenInclude(c => c!.Room)
                .Include(i => i.Contract)
                    .ThenInclude(c => c!.Tenant)
                .FirstOrDefaultAsync(i => i.InvoiceID == id.Value);

            if (invoice == null)
                return NotFound();

            return View(invoice);
        }

        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Create()
        {
            await LoadContractsAsync();

            return View(new Invoice
            {
                BillingMonth = new DateTime(
                    DateTime.Today.Year,
                    DateTime.Today.Month,
                    1),
                Status = "Unpaid"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Create(
            [Bind("ContractID,BillingMonth,ElectricOld,ElectricNew,ElectricPrice,WaterOld,WaterNew,WaterPrice,ServiceFee,Status")]
            Invoice invoice)
        {
            await ValidateInvoiceAsync(invoice, null);

            if (!ModelState.IsValid)
            {
                await LoadContractsAsync();
                return View(invoice);
            }

            var contract = await AccessibleContracts()
                .FirstOrDefaultAsync(c => c.ContractID == invoice.ContractID);

            if (contract == null)
            {
                ModelState.AddModelError(
                    nameof(invoice.ContractID),
                    "Hợp đồng không tồn tại hoặc bạn không có quyền quản lý.");

                await LoadContractsAsync();
                return View(invoice);
            }

            NormalizeInvoice(invoice, contract.RentPrice);

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã tạo hóa đơn thành công.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var invoice = await AccessibleInvoices()
                .FirstOrDefaultAsync(i => i.InvoiceID == id.Value);

            if (invoice == null)
                return NotFound();

            await LoadContractsAsync(invoice.ContractID);
            return View(invoice);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("InvoiceID,ContractID,BillingMonth,ElectricOld,ElectricNew,ElectricPrice,WaterOld,WaterNew,WaterPrice,ServiceFee,Status")]
            Invoice input)
        {
            if (id != input.InvoiceID)
                return NotFound();

            var invoice = await AccessibleInvoices()
                .FirstOrDefaultAsync(i => i.InvoiceID == id);

            if (invoice == null)
                return NotFound();

            await ValidateInvoiceAsync(input, id);

            var contract = await AccessibleContracts()
                .FirstOrDefaultAsync(c => c.ContractID == input.ContractID);

            if (contract == null)
            {
                ModelState.AddModelError(
                    nameof(input.ContractID),
                    "Hợp đồng không tồn tại hoặc bạn không có quyền quản lý.");
            }

            if (!ModelState.IsValid || contract == null)
            {
                await LoadContractsAsync(input.ContractID);
                return View(input);
            }

            invoice.ContractID = input.ContractID;
            invoice.BillingMonth = input.BillingMonth;
            invoice.ElectricOld = input.ElectricOld;
            invoice.ElectricNew = input.ElectricNew;
            invoice.ElectricPrice = input.ElectricPrice;
            invoice.WaterOld = input.WaterOld;
            invoice.WaterNew = input.WaterNew;
            invoice.WaterPrice = input.WaterPrice;
            invoice.ServiceFee = input.ServiceFee;
            invoice.Status = NormalizeStatus(input.Status);

            NormalizeInvoice(invoice, contract.RentPrice);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật hóa đơn thành công.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var invoice = await AccessibleInvoices()
                .AsNoTracking()
                .Include(i => i.Contract)
                    .ThenInclude(c => c!.Room)
                .Include(i => i.Contract)
                    .ThenInclude(c => c!.Tenant)
                .FirstOrDefaultAsync(i => i.InvoiceID == id.Value);

            if (invoice == null)
                return NotFound();

            ViewBag.CanDelete = !await HasPaymentsAsync(id.Value);
            return View(invoice);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var invoice = await AccessibleInvoices()
                .FirstOrDefaultAsync(i => i.InvoiceID == id);

            if (invoice == null)
                return NotFound();

            if (await HasPaymentsAsync(id))
            {
                TempData["Error"] =
                    "Không thể xóa hóa đơn vì đã có giao dịch thanh toán liên quan.";

                return RedirectToAction(nameof(Index));
            }

            _context.Invoices.Remove(invoice);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa hóa đơn thành công.";
            return RedirectToAction(nameof(Index));
        }

        private async Task ValidateInvoiceAsync(
            Invoice invoice,
            int? currentInvoiceId)
        {
            var contractExists = invoice.ContractID > 0 &&
                await AccessibleContracts().AnyAsync(
                    c => c.ContractID == invoice.ContractID);

            if (!contractExists)
            {
                ModelState.AddModelError(
                    nameof(invoice.ContractID),
                    "Vui lòng chọn hợp đồng hợp lệ thuộc quyền quản lý.");
            }

            if (invoice.BillingMonth == default)
            {
                ModelState.AddModelError(
                    nameof(invoice.BillingMonth),
                    "Vui lòng chọn tháng hóa đơn.");
            }

            if (invoice.ElectricOld < 0 || invoice.ElectricNew < 0)
            {
                ModelState.AddModelError(
                    nameof(invoice.ElectricOld),
                    "Chỉ số điện không được âm.");
            }

            if (invoice.ElectricNew < invoice.ElectricOld)
            {
                ModelState.AddModelError(
                    nameof(invoice.ElectricNew),
                    "Chỉ số điện mới phải lớn hơn hoặc bằng chỉ số cũ.");
            }

            if (invoice.ElectricPrice < 0)
            {
                ModelState.AddModelError(
                    nameof(invoice.ElectricPrice),
                    "Đơn giá điện không được âm.");
            }

            if (invoice.WaterOld < 0 || invoice.WaterNew < 0)
            {
                ModelState.AddModelError(
                    nameof(invoice.WaterOld),
                    "Chỉ số nước không được âm.");
            }

            if (invoice.WaterNew < invoice.WaterOld)
            {
                ModelState.AddModelError(
                    nameof(invoice.WaterNew),
                    "Chỉ số nước mới phải lớn hơn hoặc bằng chỉ số cũ.");
            }

            if (invoice.WaterPrice < 0)
            {
                ModelState.AddModelError(
                    nameof(invoice.WaterPrice),
                    "Đơn giá nước không được âm.");
            }

            if (invoice.ServiceFee < 0)
            {
                ModelState.AddModelError(
                    nameof(invoice.ServiceFee),
                    "Phí dịch vụ không được âm.");
            }

            if (string.IsNullOrWhiteSpace(invoice.Status) ||
                !AllowedStatuses.Contains(invoice.Status))
            {
                ModelState.AddModelError(
                    nameof(invoice.Status),
                    "Vui lòng chọn trạng thái hóa đơn hợp lệ.");
            }

            if (contractExists && invoice.BillingMonth != default)
            {
                var year = invoice.BillingMonth.Year;
                var month = invoice.BillingMonth.Month;

                var duplicate = await _context.Invoices.AnyAsync(i =>
                    i.ContractID == invoice.ContractID &&
                    i.BillingMonth.Year == year &&
                    i.BillingMonth.Month == month &&
                    (!currentInvoiceId.HasValue ||
                     i.InvoiceID != currentInvoiceId.Value));

                if (duplicate)
                {
                    ModelState.AddModelError(
                        nameof(invoice.BillingMonth),
                        "Hợp đồng này đã có hóa đơn trong tháng được chọn.");
                }
            }
        }

        private async Task LoadContractsAsync(
            int? selectedContractId = null)
        {
            ViewBag.Contracts = await AccessibleContracts()
                .AsNoTracking()
                .Include(c => c.Room)
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            ViewBag.SelectedContractID = selectedContractId;
        }

        private static void NormalizeInvoice(
            Invoice invoice,
            decimal rentPrice)
        {
            invoice.BillingMonth = new DateTime(
                invoice.BillingMonth.Year,
                invoice.BillingMonth.Month,
                1);

            invoice.Status = NormalizeStatus(invoice.Status);
            invoice.RentAmount = rentPrice;

            var electricCost =
                (invoice.ElectricNew - invoice.ElectricOld) *
                invoice.ElectricPrice;

            var waterCost =
                (invoice.WaterNew - invoice.WaterOld) *
                invoice.WaterPrice;

            invoice.Total = decimal.Round(
                electricCost + waterCost +
                invoice.RentAmount + invoice.ServiceFee,
                2,
                MidpointRounding.AwayFromZero);

            invoice.Contract = null;
        }

        private static string NormalizeStatus(string? status)
        {
            return AllowedStatuses.Contains(status ?? string.Empty)
                ? status!
                : "Unpaid";
        }

        private Task<bool> HasPaymentsAsync(int invoiceId)
        {
            return _context.Payments.AnyAsync(
                p => p.InvoiceID == invoiceId);
        }
    }
}
