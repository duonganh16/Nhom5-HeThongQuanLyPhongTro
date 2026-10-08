using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Data;

var builder = WebApplication.CreateBuilder(args);

// ===============================
// KẾT NỐI DATABASE SQL SERVER
// ===============================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// ===============================
// MVC
// ===============================
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ===============================
// CẤU HÌNH HTTP REQUEST PIPELINE
// ===============================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// ===============================
// ROUTE MẶC ĐỊNH
// ===============================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();