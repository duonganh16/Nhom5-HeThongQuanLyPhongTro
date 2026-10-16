using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Controllers;
using RoomRentalManagement.Data;
using RoomRentalManagement.Models;
using Xunit;

namespace RoomRentalManagement.Tests;

public class InvoicesControllerTests
{
    [Fact]
    public async Task Create_ValidInvoice_CalculatesRentUtilitiesAndTotal()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            var input = ValidInvoice(new DateTime(2026, 10, 19));

            var result = await controller.Create(input);

            Assert.IsType<RedirectToActionResult>(result);
            var saved = await db.Invoices.SingleAsync();
            Assert.Equal(new DateTime(2026, 10, 1), saved.BillingMonth);
            Assert.Equal(1_000_000m, saved.RentAmount);
            Assert.Equal(105_000m, (saved.ElectricNew - saved.ElectricOld) * saved.ElectricPrice);
            Assert.Equal(75_000m, (saved.WaterNew - saved.WaterOld) * saved.WaterPrice);
            Assert.Equal(1_230_000m, saved.Total);
            Assert.Equal("Unpaid", saved.Status);
        }
    }

    [Fact]
    public async Task Create_ElectricReadingDecreased_ReturnsViewAndDoesNotSave()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            var input = ValidInvoice();
            input.ElectricOld = 150;
            input.ElectricNew = 149;

            var result = await controller.Create(input);

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(nameof(Invoice.ElectricNew), controller.ModelState.Keys);
            Assert.Equal(0, await db.Invoices.CountAsync());
        }
    }

    [Fact]
    public async Task Create_WaterReadingDecreased_ReturnsViewAndDoesNotSave()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            var input = ValidInvoice();
            input.WaterOld = 30;
            input.WaterNew = 29;

            var result = await controller.Create(input);

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(nameof(Invoice.WaterNew), controller.ModelState.Keys);
            Assert.Equal(0, await db.Invoices.CountAsync());
        }
    }

    [Fact]
    public async Task Create_NegativeServiceFee_ReturnsViewAndDoesNotSave()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            var input = ValidInvoice();
            input.ServiceFee = -1;

            var result = await controller.Create(input);

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(nameof(Invoice.ServiceFee), controller.ModelState.Keys);
            Assert.Equal(0, await db.Invoices.CountAsync());
        }
    }

    [Fact]
    public async Task Create_DuplicateContractAndMonth_ReturnsViewAndDoesNotSaveSecondInvoice()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            db.Invoices.Add(new Invoice
            {
                InvoiceID = 10,
                ContractID = 1,
                BillingMonth = new DateTime(2026, 10, 1),
                Status = "Unpaid"
            });
            await db.SaveChangesAsync();

            var input = ValidInvoice(new DateTime(2026, 10, 25));
            var result = await controller.Create(input);

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(nameof(Invoice.BillingMonth), controller.ModelState.Keys);
            Assert.Equal(1, await db.Invoices.CountAsync());
        }
    }

    [Fact]
    public async Task Create_UnknownContract_ReturnsViewAndDoesNotSave()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            var input = ValidInvoice();
            input.ContractID = 999;

            var result = await controller.Create(input);

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(nameof(Invoice.ContractID), controller.ModelState.Keys);
            Assert.Equal(0, await db.Invoices.CountAsync());
        }
    }

    [Fact]
    public async Task Edit_ValidInvoice_RecalculatesAmountsUsingContractRentPrice()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            db.Invoices.Add(new Invoice
            {
                InvoiceID = 1,
                ContractID = 1,
                BillingMonth = new DateTime(2026, 9, 1),
                ElectricOld = 0,
                ElectricNew = 0,
                ElectricPrice = 0,
                WaterOld = 0,
                WaterNew = 0,
                WaterPrice = 0,
                RentAmount = 1_000_000m,
                ServiceFee = 0,
                Total = 1_000_000m,
                Status = "Unpaid"
            });
            await db.SaveChangesAsync();

            var input = ValidInvoice(new DateTime(2026, 11, 15));
            input.InvoiceID = 1;
            input.Status = "Paid";

            var result = await controller.Edit(1, input);

            Assert.IsType<RedirectToActionResult>(result);
            var saved = await db.Invoices.SingleAsync();
            Assert.Equal(new DateTime(2026, 11, 1), saved.BillingMonth);
            Assert.Equal(1_000_000m, saved.RentAmount);
            Assert.Equal(1_230_000m, saved.Total);
            Assert.Equal("Paid", saved.Status);
        }
    }

    [Fact]
    public async Task DeleteConfirmed_WhenInvoiceHasPayment_DoesNotDeleteInvoice()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            db.Invoices.Add(new Invoice
            {
                InvoiceID = 1,
                ContractID = 1,
                BillingMonth = new DateTime(2026, 10, 1),
                Status = "Unpaid"
            });
            db.Payments.Add(new Payment
            {
                PaymentID = 1,
                InvoiceID = 1,
                PaymentDate = new DateTime(2026, 10, 5),
                Amount = 100_000m,
                PaymentMethod = "Cash"
            });
            await db.SaveChangesAsync();

            var result = await controller.DeleteConfirmed(1);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(1, await db.Invoices.CountAsync());
            Assert.True(controller.TempData.ContainsKey("Error"));
        }
    }

    [Fact]
    public async Task DeleteConfirmed_WhenInvoiceHasNoPayments_DeletesInvoice()
    {
        var (db, controller) = await CreateSutAsync();
        await using (db)
        {
            db.Invoices.Add(new Invoice
            {
                InvoiceID = 1,
                ContractID = 1,
                BillingMonth = new DateTime(2026, 10, 1),
                Status = "Unpaid"
            });
            await db.SaveChangesAsync();

            var result = await controller.DeleteConfirmed(1);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(0, await db.Invoices.CountAsync());
            Assert.True(controller.TempData.ContainsKey("Success"));
        }
    }

    private static Invoice ValidInvoice(DateTime? billingMonth = null) => new()
    {
        ContractID = 1,
        BillingMonth = billingMonth ?? new DateTime(2026, 10, 15),
        ElectricOld = 100m,
        ElectricNew = 130m,
        ElectricPrice = 3_500m,
        WaterOld = 20m,
        WaterNew = 25m,
        WaterPrice = 15_000m,
        ServiceFee = 50_000m,
        Status = "Unpaid"
    };

    private static async Task<(ApplicationDbContext Db, InvoicesController Controller)> CreateSutAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"InvoiceTests-{Guid.NewGuid()}")
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
            RentPrice = 1_000_000m,
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        var controller = new InvoicesController(db)
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
            // No persistence needed for controller unit tests.
        }
    }
}
