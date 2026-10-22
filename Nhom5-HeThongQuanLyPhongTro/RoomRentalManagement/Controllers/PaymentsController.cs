
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Data;
using RoomRentalManagement.Models;

namespace RoomRentalManagement.Controllers
{
    [Authorize(Roles = "Admin,Landlord,Tenant")]
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        private static readonly string[] AllowedMethods =
        {
            "Tiền mặt",
            "Chuyển khoản",
            "Ví điện tử",
            "Khác"
        };

        public PaymentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int CurrentUserID =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Hóa đơn mà tài khoản hiện tại được phép truy cập.
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

        // Giao dịch chỉ thuộc phạm vi hóa đơn được phép truy cập.
        private IQueryable<Payment> AccessiblePayments()
        {
            var query = _context.Payments.AsQueryable();

            if (User.IsInRole("Admin"))
                return query;

            if (User.IsInRole("Landlord"))
            {
                return query.Where(p =>
                    p.Invoice != null &&
                    p.Invoice.Contract != null &&
                    p.Invoice.Contract.Room != null &&
                    p.Invoice.Contract.Room.LandlordID == CurrentUserID);
            }

            if (User.IsInRole("Tenant"))
            {
                return query.Where(p =>
                    p.Invoice != null &&
                    p.Invoice.Contract != null &&
                    p.Invoice.Contract.Tenant != null &&
                    p.Invoice.Contract.Tenant.UserID == CurrentUserID);
            }

            return query.Where(p => false);
        }

        // Danh sách giao dịch.
        public async Task<IActionResult> Index()
        {
            var payments = await PaymentQuery()
                .OrderByDescending(p => p.PaymentDate)
                .ThenByDescending(p => p.PaymentID)
                .ToListAsync();

            return View(payments);
        }

        // Chi tiết giao dịch.
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var payment = await PaymentQuery()
                .FirstOrDefaultAsync(p => p.PaymentID == id.Value);

            if (payment == null)
                return NotFound();

            return View(payment);
        }

        // Chỉ Admin và Chủ nhà được ghi nhận thanh toán.
        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Create()
        {
            await LoadInvoicesAsync();

            return View(new Payment
            {
                PaymentDate = DateTime.Today,
                PaymentMethod = AllowedMethods[0]
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Create(
            [Bind("InvoiceID,PaymentDate,Amount,PaymentMethod,Note")]
            Payment payment)
        {
            var invoice = await ValidatePaymentAsync(payment, null);

            if (!ModelState.IsValid || invoice == null)
            {
                await LoadInvoicesAsync();
                return View(payment);
            }

            var paidBefore = await _context.Payments
                .Where(p => p.InvoiceID == payment.InvoiceID)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            payment.PaymentMethod = NormalizeMethod(payment.PaymentMethod);
            payment.Note = string.IsNullOrWhiteSpace(payment.Note)
                ? null
                : payment.Note.Trim();

            payment.Invoice = null;

            invoice.Status = CalculateInvoiceStatus(
                invoice, paidBefore + payment.Amount);

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã ghi nhận thanh toán thành công.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var payment = await AccessiblePayments()
                .FirstOrDefaultAsync(p => p.PaymentID == id.Value);

            if (payment == null)
                return NotFound();

            await LoadInvoicesAsync(payment.InvoiceID);
            return View(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("PaymentID,InvoiceID,PaymentDate,Amount,PaymentMethod,Note")]
            Payment input)
        {
            if (id != input.PaymentID)
                return NotFound();

            var payment = await AccessiblePayments()
                .FirstOrDefaultAsync(p => p.PaymentID == id);

            if (payment == null)
                return NotFound();

            var targetInvoice = await ValidatePaymentAsync(input, id);

            if (!ModelState.IsValid || targetInvoice == null)
            {
                await LoadInvoicesAsync(input.InvoiceID);
                return View(input);
            }

            var oldInvoiceId = payment.InvoiceID;

            var paidForTargetWithoutCurrent = await _context.Payments
                .Where(p => p.InvoiceID == input.InvoiceID &&
                            p.PaymentID != id)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            payment.InvoiceID = input.InvoiceID;
            payment.PaymentDate = input.PaymentDate;
            payment.Amount = input.Amount;
            payment.PaymentMethod = NormalizeMethod(input.PaymentMethod);
            payment.Note = string.IsNullOrWhiteSpace(input.Note)
                ? null
                : input.Note.Trim();

            targetInvoice.Status = CalculateInvoiceStatus(
                targetInvoice,
                paidForTargetWithoutCurrent + input.Amount);

            if (oldInvoiceId != input.InvoiceID)
            {
                var oldInvoice = await AccessibleInvoices()
                    .FirstOrDefaultAsync(i => i.InvoiceID == oldInvoiceId);

                if (oldInvoice != null)
                {
                    var oldInvoicePaid = await _context.Payments
                        .Where(p => p.InvoiceID == oldInvoiceId &&
                                    p.PaymentID != id)
                        .SumAsync(p => (decimal?)p.Amount) ?? 0m;

                    oldInvoice.Status = CalculateInvoiceStatus(
                        oldInvoice, oldInvoicePaid);
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật thanh toán thành công.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var payment = await PaymentQuery()
                .FirstOrDefaultAsync(p => p.PaymentID == id.Value);

            if (payment == null)
                return NotFound();

            return View(payment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Landlord")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var payment = await AccessiblePayments()
                .FirstOrDefaultAsync(p => p.PaymentID == id);

            if (payment == null)
                return NotFound();

            var invoiceId = payment.InvoiceID;

            var invoice = await AccessibleInvoices()
                .FirstOrDefaultAsync(i => i.InvoiceID == invoiceId);

            var paidWithoutCurrent = await _context.Payments
                .Where(p => p.InvoiceID == invoiceId &&
                            p.PaymentID != id)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            if (invoice != null)
            {
                invoice.Status = CalculateInvoiceStatus(
                    invoice, paidWithoutCurrent);
            }

            _context.Payments.Remove(payment);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa giao dịch thanh toán.";
            return RedirectToAction(nameof(Index));
        }

        private IQueryable<Payment> PaymentQuery()
        {
            return AccessiblePayments()
                .AsNoTracking()
                .Include(p => p.Invoice)
                    .ThenInclude(i => i!.Contract)
                        .ThenInclude(c => c!.Room)
                .Include(p => p.Invoice)
                    .ThenInclude(i => i!.Contract)
                        .ThenInclude(c => c!.Tenant);
        }

        private async Task<Invoice?> ValidatePaymentAsync(
            Payment payment,
            int? currentPaymentId)
        {
            Invoice? invoice = null;

            if (payment.InvoiceID > 0)
            {
                invoice = await AccessibleInvoices()
                    .FirstOrDefaultAsync(
                        i => i.InvoiceID == payment.InvoiceID);
            }

            if (invoice == null)
            {
                ModelState.AddModelError(
                    nameof(payment.InvoiceID),
                    "Hóa đơn không tồn tại hoặc bạn không có quyền quản lý.");
            }

            if (payment.PaymentDate == default)
            {
                ModelState.AddModelError(
                    nameof(payment.PaymentDate),
                    "Vui lòng nhập ngày thanh toán.");
            }
            else if (payment.PaymentDate.Date > DateTime.Today)
            {
                ModelState.AddModelError(
                    nameof(payment.PaymentDate),
                    "Ngày thanh toán không được ở tương lai.");
            }

            if (payment.Amount <= 0)
            {
                ModelState.AddModelError(
                    nameof(payment.Amount),
                    "Số tiền thanh toán phải lớn hơn 0.");
            }

            if (string.IsNullOrWhiteSpace(payment.PaymentMethod) ||
                !AllowedMethods.Contains(
                    payment.PaymentMethod.Trim(),
                    StringComparer.Ordinal))
            {
                ModelState.AddModelError(
                    nameof(payment.PaymentMethod),
                    "Vui lòng chọn phương thức thanh toán hợp lệ.");
            }

            if (payment.Note?.Length > 500)
            {
                ModelState.AddModelError(
                    nameof(payment.Note),
                    "Ghi chú không được vượt quá 500 ký tự.");
            }

            if (invoice != null && payment.Amount > 0)
            {
                var existingPaid = _context.Payments
                    .Where(p => p.InvoiceID == invoice.InvoiceID);

                if (currentPaymentId.HasValue)
                {
                    existingPaid = existingPaid.Where(
                        p => p.PaymentID != currentPaymentId.Value);
                }

                var paidBefore =
                    await existingPaid.SumAsync(p => (decimal?)p.Amount)
                    ?? 0m;

                if (paidBefore + payment.Amount > invoice.Total)
                {
                    var remaining = Math.Max(
                        0m, invoice.Total - paidBefore);

                    ModelState.AddModelError(
                        nameof(payment.Amount),
                        $"Số tiền vượt quá số còn phải thu ({remaining:N2} đ).");
                }
            }

            return invoice;
        }

        private async Task LoadInvoicesAsync(
            int? selectedInvoiceId = null)
        {
            ViewBag.Invoices = await AccessibleInvoices()
                .AsNoTracking()
                .Include(i => i.Contract)
                    .ThenInclude(c => c!.Room)
                .Include(i => i.Contract)
                    .ThenInclude(c => c!.Tenant)
                .OrderByDescending(i => i.BillingMonth)
                .ThenByDescending(i => i.InvoiceID)
                .ToListAsync();

            ViewBag.SelectedInvoiceID = selectedInvoiceId;
            ViewBag.PaymentMethods = AllowedMethods;
        }

        private static string NormalizeMethod(string? method)
        {
            var normalized = (method ?? string.Empty).Trim();

            return AllowedMethods.Contains(
                normalized, StringComparer.Ordinal)
                ? normalized
                : AllowedMethods[0];
        }

        private static string CalculateInvoiceStatus(
            Invoice invoice,
            decimal paidAmount)
        {
            if (paidAmount >= invoice.Total)
                return "Paid";

            return invoice.Status == "Overdue"
                ? "Overdue"
                : "Unpaid";
        }
    }
}
