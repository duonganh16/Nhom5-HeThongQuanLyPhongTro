# AN CƯ HOME – Hệ thống quản lý phòng trọ

**AN CƯ HOME** là hệ thống quản lý phòng trọ được xây dựng bằng **ASP.NET Core MVC** và **SQL Server**, hỗ trợ quản lý phòng trọ, người thuê, hợp đồng, hóa đơn, thanh toán và yêu cầu hỗ trợ.

---

## 1. Giới thiệu dự án

Hệ thống được phát triển nhằm hỗ trợ công tác quản lý phòng trọ một cách thuận tiện, hiệu quả và có tổ chức. Dự án áp dụng mô hình ASP.NET Core MVC kết hợp với cơ sở dữ liệu SQL Server.

## 2. Công nghệ sử dụng

| Công nghệ | Mô tả |
|---|---|
| Backend | ASP.NET Core MVC, C# |
| Database | SQL Server |
| ORM | Entity Framework Core |
| Frontend | HTML, CSS, JavaScript, Bootstrap |
| IDE | Visual Studio |
| Quản lý mã nguồn | Git, GitHub |

## 3. Chức năng chính

- **Quản lý tài khoản:** Đăng nhập, đăng ký và phân quyền người dùng.
- **Quản lý phòng trọ:** Theo dõi thông tin và tình trạng phòng.
- **Quản lý người thuê:** Lưu trữ và quản lý thông tin người thuê.
- **Quản lý hợp đồng:** Theo dõi thông tin hợp đồng thuê phòng.
- **Quản lý hóa đơn:** Quản lý hóa đơn tiền phòng và các khoản phí.
- **Quản lý thanh toán:** Theo dõi các khoản thanh toán.
- **Quản lý yêu cầu:** Tiếp nhận và xử lý yêu cầu hỗ trợ.
- **Thống kê:** Tổng hợp thông tin phục vụ công tác quản lý.

## 4. Đối tượng sử dụng

- **Admin:** Quản lý hệ thống và người dùng.
- **Chủ trọ (Landlord):** Quản lý phòng trọ, người thuê và các hoạt động liên quan.
- **Người thuê (Tenant):** Theo dõi thông tin thuê phòng và các chức năng được cấp quyền.

## 5. Cài đặt và chạy dự án

### Bước 1: Clone repository

```bash
git clone https://github.com/duonganh16/Nhom5-HeThongQuanLyPhongTro.git
```

### Bước 2: Mở dự án

Mở file solution `.slnx` bằng Visual Studio.

### Bước 3: Cấu hình cơ sở dữ liệu

Kiểm tra chuỗi kết nối trong `appsettings.json` và cấu hình SQL Server phù hợp với máy đang chạy.

### Bước 4: Cập nhật cơ sở dữ liệu

Mở Terminal tại thư mục chứa file `.csproj`, sau đó chạy:

```bash
dotnet ef database update
```

Nếu máy chưa cài công cụ EF Core CLI, cần cài đặt công cụ phù hợp trước khi chạy lệnh.

### Bước 5: Chạy ứng dụng

Trong Visual Studio, chọn dự án làm Startup Project và nhấn **F5** hoặc **Ctrl + F5**.

## 6. Cấu trúc dự án

| Thư mục | Chức năng |
|---|---|
| `Controllers/` | Xử lý yêu cầu và điều hướng chức năng |
| `Models/` | Các lớp dữ liệu của hệ thống |
| `Data/` | Cấu hình DbContext |
| `Migrations/` | Các migration của Entity Framework Core |
| `Views/` | Giao diện Razor |
| `wwwroot/` | CSS, JavaScript và tài nguyên giao diện |

## 7. Repository

GitHub: [Nhom5-HeThongQuanLyPhongTro](https://github.com/duonganh16/Nhom5-HeThongQuanLyPhongTro)

---

<p align="center">
   <strong>AN CƯ HOME</strong><br>
  <em>Giải pháp quản lý phòng trọ tiện lợi và hiệu quả.</em>
</p>
