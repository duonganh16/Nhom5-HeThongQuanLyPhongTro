using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Data;
using RoomRentalManagement.Models;

namespace RoomRentalManagement.Controllers
{
    public class InvoicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private static readonly string[] AllowedStatuses = { "Unpaid", "Paid", "Overdue" };

        public InvoicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Invoices
        public async Task<IActionResult> Index()
        {
            var invoices = await _context.Invoices
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

        // GET: Invoices/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var invoice = await _context.Invoices
                .AsNoTracking()
                .Include(i => i.Contract)
                    .ThenInclude(c => c!.Room)
                .Include(i => i.Contract)
                    .ThenInclude(c => c!.Tenant)
                .FirstOrDefaultAsync(i => i.InvoiceID == id.Value);

            if (invoice == null) return NotFound();
            return View(invoice);
        }

        // GET: Invoices/Create
        public async Task<IActionResult> Create()
        {
            await LoadContractsAsync();
            return View(new Invoice
            {
                BillingMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                Status = "Unpaid"
            });
        }

        // POST: Invoices/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("ContractID,BillingMonth,ElectricOld,ElectricNew,ElectricPrice,WaterOld,WaterNew,WaterPrice,ServiceFee,Status")] Invoice invoice)
        {
            await ValidateInvoiceAsync(invoice, null);

            if (!ModelState.IsValid)
            {
                await LoadContractsAsync();
                return View(invoice);
            }

            var contract = await _context.Contracts.FindAsync(invoice.ContractID);
            if (contract == null)
            {
                ModelState.AddModelError(nameof(invoice.ContractID), "Hợp đồng đã chọn không tồn tại.");
                await LoadContractsAsync();
                return View(invoice);
            }

            NormalizeInvoice(invoice, contract.RentPrice);
            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã tạo hóa đơn thành công.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Invoices/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var invoice = await _context.Invoices.FindAsync(id.Value);
            if (invoice == null) return NotFound();

            await LoadContractsAsync();
            return View(invoice);
        }

        // POST: Invoices/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("InvoiceID,ContractID,BillingMonth,ElectricOld,ElectricNew,ElectricPrice,WaterOld,WaterNew,WaterPrice,ServiceFee,Status")] Invoice input)
        {
            if (id != input.InvoiceID) return NotFound();

            await ValidateInvoiceAsync(input, id);
            if (!ModelState.IsValid)
            {
                await LoadContractsAsync();
                return View(input);
            }

            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceID == id);
            if (invoice == null) return NotFound();

            var contract = await _context.Contracts.FindAsync(input.ContractID);
            if (contract == null)
            {
                ModelState.AddModelError(nameof(input.ContractID), "Hợp đồng đã chọn không tồn tại.");
                await LoadContractsAsync();
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

        // GET: Invoices/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var invoice = await _context.Invoices
                .AsNoTracking()
                .Include(i => i.Contract)
                    .ThenInclude(c => c!.Room)
                .Include(i => i.Contract)
                    .ThenInclude(c => c!.Tenant)
                .FirstOrDefaultAsync(i => i.InvoiceID == id.Value);

            if (invoice == null) return NotFound();

            ViewBag.CanDelete = !await HasPaymentsAsync(id.Value);
            return View(invoice);
        }

        // POST: Invoices/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null) return NotFound();

            // Preserve payment history. Do not delete an invoice that already has payments.
            if (await HasPaymentsAsync(id))
            {
                TempData["Error"] = "Không thể xóa hóa đơn này vì đã có giao dịch thanh toán liên quan.";
                return RedirectToAction(nameof(Index));
            }

            _context.Invoices.Remove(invoice);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa hóa đơn thành công.";
            return RedirectToAction(nameof(Index));
        }

        private async Task ValidateInvoiceAsync(Invoice invoice, int? currentInvoiceId)
        {
            var contractExists = invoice.ContractID > 0 &&
                await _context.Contracts.AnyAsync(c => c.ContractID == invoice.ContractID);

            if (!contractExists)
                ModelState.AddModelError(nameof(invoice.ContractID), "Vui lòng chọn hợp đồng hợp lệ.");

            if (invoice.BillingMonth == default)
                ModelState.AddModelError(nameof(invoice.BillingMonth), "Vui lòng chọn tháng hóa đơn.");

            if (invoice.ElectricOld < 0 || invoice.ElectricNew < 0)
                ModelState.AddModelError(nameof(invoice.ElectricOld), "Chỉ số điện không được âm.");

            if (invoice.ElectricNew < invoice.ElectricOld)
                ModelState.AddModelError(nameof(invoice.ElectricNew), "Chỉ số điện mới phải lớn hơn hoặc bằng chỉ số cũ.");

            if (invoice.ElectricPrice < 0)
                ModelState.AddModelError(nameof(invoice.ElectricPrice), "Đơn giá điện không được âm.");

            if (invoice.WaterOld < 0 || invoice.WaterNew < 0)
                ModelState.AddModelError(nameof(invoice.WaterOld), "Chỉ số nước không được âm.");

            if (invoice.WaterNew < invoice.WaterOld)
                ModelState.AddModelError(nameof(invoice.WaterNew), "Chỉ số nước mới phải lớn hơn hoặc bằng chỉ số cũ.");

            if (invoice.WaterPrice < 0)
                ModelState.AddModelError(nameof(invoice.WaterPrice), "Đơn giá nước không được âm.");

            if (invoice.ServiceFee < 0)
                ModelState.AddModelError(nameof(invoice.ServiceFee), "Phí dịch vụ không được âm.");

            if (string.IsNullOrWhiteSpace(invoice.Status) || !AllowedStatuses.Contains(invoice.Status))
                ModelState.AddModelError(nameof(invoice.Status), "Vui lòng chọn trạng thái hóa đơn hợp lệ.");

            if (contractExists && invoice.BillingMonth != default)
            {
                var year = invoice.BillingMonth.Year;
                var month = invoice.BillingMonth.Month;
                var duplicate = await _context.Invoices.AnyAsync(i =>
                    i.ContractID == invoice.ContractID &&
                    i.BillingMonth.Year == year &&
                    i.BillingMonth.Month == month &&
                    (!currentInvoiceId.HasValue || i.InvoiceID != currentInvoiceId.Value));

                if (duplicate)
                    ModelState.AddModelError(nameof(invoice.BillingMonth), "Hợp đồng này đã có hóa đơn trong tháng được chọn.");
            }
        }

        private async Task LoadContractsAsync()
        {
            ViewBag.Contracts = await _context.Contracts
                .AsNoTracking()
                .Include(c => c.Room)
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();
        }

        private static void NormalizeInvoice(Invoice invoice, decimal rentPrice)
        {
            invoice.BillingMonth = new DateTime(invoice.BillingMonth.Year, invoice.BillingMonth.Month, 1);
            invoice.Status = NormalizeStatus(invoice.Status);
            invoice.RentAmount = rentPrice;

            var electricCost = (invoice.ElectricNew - invoice.ElectricOld) * invoice.ElectricPrice;
            var waterCost = (invoice.WaterNew - invoice.WaterOld) * invoice.WaterPrice;
            invoice.Total = decimal.Round(
                electricCost + waterCost + invoice.RentAmount + invoice.ServiceFee,
                2,
                MidpointRounding.AwayFromZero);

            // Navigation properties are resolved by EF Core; they are never accepted from form input.
            invoice.Contract = null;
        }

        private static string NormalizeStatus(string? status)
        {
            return AllowedStatuses.Contains(status ?? string.Empty) ? status! : "Unpaid";
        }

        private Task<bool> HasPaymentsAsync(int invoiceId)
        {
            return _context.Payments.AnyAsync(p => p.InvoiceID == invoiceId);
        }
    }
}
