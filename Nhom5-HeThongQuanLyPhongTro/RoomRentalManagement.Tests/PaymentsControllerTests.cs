using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Controllers;
using RoomRentalManagement.Data;
using RoomRentalManagement.Models;
using Xunit;

namespace RoomRentalManagement.Tests;

public class PaymentsControllerTests
{
    [Fact]
    public async Task Create_ValidPartialPayment_SavesPaymentAndKeepsInvoiceUnpaid()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            var result = await controller.Create(ValidPayment(amount: 200m));

            Assert.IsType<RedirectToActionResult>(result);
            var savedPayment = await db.Payments.SingleAsync();
            var savedInvoice = await db.Invoices.SingleAsync(i => i.InvoiceID == 1);

            Assert.Equal(200m, savedPayment.Amount);
            Assert.Equal("Tiền mặt", savedPayment.PaymentMethod);
            Assert.Equal("Partial payment", savedPayment.Note);
            Assert.Equal("Unpaid", savedInvoice.Status);
        }
    }

    [Fact]
    public async Task Create_FullPayment_MarksInvoicePaid()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            var result = await controller.Create(ValidPayment(amount: 500m));

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(1, await db.Payments.CountAsync());
            Assert.Equal("Paid", (await db.Invoices.SingleAsync(i => i.InvoiceID == 1)).Status);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_NonPositiveAmount_ReturnsViewAndDoesNotSave(decimal amount)
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            var result = await controller.Create(ValidPayment(amount));

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(nameof(Payment.Amount), controller.ModelState.Keys);
            Assert.Empty(await db.Payments.ToListAsync());
        }
    }

    [Fact]
    public async Task Create_FuturePaymentDate_ReturnsViewAndDoesNotSave()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            var input = ValidPayment(100m);
            input.PaymentDate = DateTime.Today.AddDays(1);

            var result = await controller.Create(input);

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(nameof(Payment.PaymentDate), controller.ModelState.Keys);
            Assert.Empty(await db.Payments.ToListAsync());
        }
    }

    [Fact]
    public async Task Create_InvalidPaymentMethod_ReturnsViewAndDoesNotSave()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            var input = ValidPayment(100m);
            input.PaymentMethod = "Unknown method";

            var result = await controller.Create(input);

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(nameof(Payment.PaymentMethod), controller.ModelState.Keys);
            Assert.Empty(await db.Payments.ToListAsync());
        }
    }

    [Fact]
    public async Task Create_UnknownInvoice_ReturnsViewAndDoesNotSave()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            var input = ValidPayment(100m);
            input.InvoiceID = 999;

            var result = await controller.Create(input);

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(nameof(Payment.InvoiceID), controller.ModelState.Keys);
            Assert.Empty(await db.Payments.ToListAsync());
        }
    }

    [Fact]
    public async Task Create_AmountExceedsRemainingBalance_ReturnsViewAndDoesNotSave()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            db.Payments.Add(new Payment
            {
                PaymentID = 1,
                InvoiceID = 1,
                PaymentDate = DateTime.Today,
                Amount = 350m,
                PaymentMethod = "Tiền mặt"
            });
            await db.SaveChangesAsync();

            var result = await controller.Create(ValidPayment(200m));

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(nameof(Payment.Amount), controller.ModelState.Keys);
            Assert.Equal(1, await db.Payments.CountAsync());
        }
    }

    [Fact]
    public async Task Create_ValidPayment_TrimsNoteAndMethod()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            var input = ValidPayment(100m);
            input.PaymentMethod = "  Chuyển khoản  ";
            input.Note = "  Paid via bank transfer  ";

            var result = await controller.Create(input);

            Assert.IsType<RedirectToActionResult>(result);
            var saved = await db.Payments.SingleAsync();
            Assert.Equal("Chuyển khoản", saved.PaymentMethod);
            Assert.Equal("Paid via bank transfer", saved.Note);
        }
    }

    [Fact]
    public async Task Edit_ValidPaymentChangingToFullPayment_MarksInvoicePaid()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            db.Payments.Add(new Payment
            {
                PaymentID = 10,
                InvoiceID = 1,
                PaymentDate = DateTime.Today,
                Amount = 100m,
                PaymentMethod = "Tiền mặt"
            });
            await db.SaveChangesAsync();

            var input = ValidPayment(500m);
            input.PaymentID = 10;

            var result = await controller.Edit(10, input);

            Assert.IsType<RedirectToActionResult>(result);
            var savedPayment = await db.Payments.SingleAsync(p => p.PaymentID == 10);
            var savedInvoice = await db.Invoices.SingleAsync(i => i.InvoiceID == 1);
            Assert.Equal(500m, savedPayment.Amount);
            Assert.Equal("Paid", savedInvoice.Status);
        }
    }

    [Fact]
    public async Task Edit_AmountWouldExceedRemainingBalance_ReturnsViewAndKeepsPaymentUnchanged()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            db.Payments.AddRange(
                new Payment
                {
                    PaymentID = 10,
                    InvoiceID = 1,
                    PaymentDate = DateTime.Today,
                    Amount = 100m,
                    PaymentMethod = "Tiền mặt"
                },
                new Payment
                {
                    PaymentID = 11,
                    InvoiceID = 1,
                    PaymentDate = DateTime.Today,
                    Amount = 350m,
                    PaymentMethod = "Chuyển khoản"
                });
            await db.SaveChangesAsync();

            var input = ValidPayment(200m);
            input.PaymentID = 10;

            var result = await controller.Edit(10, input);

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(nameof(Payment.Amount), controller.ModelState.Keys);
            Assert.Equal(100m, (await db.Payments.SingleAsync(p => p.PaymentID == 10)).Amount);
        }
    }

    [Fact]
    public async Task Edit_MovingPayment_RecalculatesBothInvoiceStatuses()
    {
        var (db, controller) = await CreateSutAsync(includeSecondInvoice: true);
        await using (db)
        {
            var firstInvoice = await db.Invoices.SingleAsync(i => i.InvoiceID == 1);
            firstInvoice.Status = "Paid";
            db.Payments.Add(new Payment
            {
                PaymentID = 10,
                InvoiceID = 1,
                PaymentDate = DateTime.Today,
                Amount = 500m,
                PaymentMethod = "Tiền mặt"
            });
            await db.SaveChangesAsync();

            var input = ValidPayment(300m);
            input.PaymentID = 10;
            input.InvoiceID = 2;

            var result = await controller.Edit(10, input);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(2, (await db.Payments.SingleAsync(p => p.PaymentID == 10)).InvoiceID);
            Assert.Equal("Unpaid", (await db.Invoices.SingleAsync(i => i.InvoiceID == 1)).Status);
            Assert.Equal("Unpaid", (await db.Invoices.SingleAsync(i => i.InvoiceID == 2)).Status);
        }
    }

    [Fact]
    public async Task DeleteConfirmed_DeletingLastPayment_RecalculatesInvoiceAsUnpaid()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            var invoice = await db.Invoices.SingleAsync(i => i.InvoiceID == 1);
            invoice.Status = "Paid";
            db.Payments.Add(new Payment
            {
                PaymentID = 10,
                InvoiceID = 1,
                PaymentDate = DateTime.Today,
                Amount = 500m,
                PaymentMethod = "Tiền mặt"
            });
            await db.SaveChangesAsync();

            var result = await controller.DeleteConfirmed(10);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Empty(await db.Payments.ToListAsync());
            Assert.Equal("Unpaid", (await db.Invoices.SingleAsync(i => i.InvoiceID == 1)).Status);
        }
    }

    [Fact]
    public async Task DeleteConfirmed_WhenPartialPaymentRemains_InvoiceStaysUnpaid()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            db.Payments.AddRange(
                new Payment
                {
                    PaymentID = 10,
                    InvoiceID = 1,
                    PaymentDate = DateTime.Today,
                    Amount = 200m,
                    PaymentMethod = "Tiền mặt"
                },
                new Payment
                {
                    PaymentID = 11,
                    InvoiceID = 1,
                    PaymentDate = DateTime.Today,
                    Amount = 300m,
                    PaymentMethod = "Chuyển khoản"
                });
            await db.SaveChangesAsync();
            var invoice = await db.Invoices.SingleAsync(i => i.InvoiceID == 1);
            invoice.Status = "Paid";
            await db.SaveChangesAsync();

            var result = await controller.DeleteConfirmed(10);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(1, await db.Payments.CountAsync());
            Assert.Equal("Unpaid", (await db.Invoices.SingleAsync(i => i.InvoiceID == 1)).Status);
        }
    }

    private static Payment ValidPayment(decimal amount) => new()
    {
        InvoiceID = 1,
        PaymentDate = DateTime.Today,
        Amount = amount,
        PaymentMethod = "Tiền mặt",
        Note = "Partial payment"
    };

    private static async Task<(ApplicationDbContext Db, PaymentsController Controller)> CreateSutAsync(
        bool includeSecondInvoice = false)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"PaymentTests-{Guid.NewGuid()}")
            .Options;

        var db = new ApplicationDbContext(options);
        await db.Contracts.AddAsync(new Contract
        {
            ContractID = 1,
            RoomID = 1,
            TenantID = 1,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = null,
            Deposit = 0m,
            RentPrice = 500m,
            Status = "Active"
        });

        await db.Invoices.AddAsync(new Invoice
        {
            InvoiceID = 1,
            ContractID = 1,
            BillingMonth = new DateTime(2026, 10, 1),
            RentAmount = 500m,
            Total = 500m,
            Status = "Unpaid"
        });

        if (includeSecondInvoice)
        {
            await db.Invoices.AddAsync(new Invoice
            {
                InvoiceID = 2,
                ContractID = 1,
                BillingMonth = new DateTime(2026, 11, 1),
                RentAmount = 500m,
                Total = 500m,
                Status = "Unpaid"
            });
        }

        await db.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        var controller = new PaymentsController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };

        return (db, controller);
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
            => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            // No persistence is needed in controller unit tests.
        }
    }
}
