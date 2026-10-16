
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RoomRentalManagement.Data;
using RoomRentalManagement.Models;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// 1. KET NOI DATABASE SQL SERVER
// ========================================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// ========================================
// 2. CAU HINH MVC
// ========================================
builder.Services.AddControllersWithViews();

// ========================================
// 3. DICH VU BAM MAT KHAU
// ========================================
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// ========================================
// 4. XAC THUC BANG COOKIE
// ========================================
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";

        options.Cookie.Name = "RoomRentalManagement.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// ========================================
// 5. PHAN QUYEN
// ========================================
builder.Services.AddAuthorization();

var app = builder.Build();

// ========================================
// 6. HTTP REQUEST PIPELINE
// ========================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// Xac thuc phai dung truoc phan quyen
app.UseAuthentication();
app.UseAuthorization();

// ========================================
// 7. ROUTE MAC DINH
// ========================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
