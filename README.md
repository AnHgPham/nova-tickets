# NOVA Tickets

Website bán vé sự kiện ca nhạc full-stack bằng **ASP.NET Core 8** và **React**. Xem chi tiết endpoint, bảo mật và luồng chống trùng ghế trong [API.md](./API.md); kiến trúc dữ liệu trong [ARCHITECTURE.md](./ARCHITECTURE.md); cách dùng database trong [DATABASE_USAGE.md](./DATABASE_USAGE.md).

## Cài đặt

Máy cần có .NET SDK 8, Node.js 22, pnpm 10 và MySQL 8 (hoặc TiDB tương thích MySQL).

```bash
corepack enable
corepack prepare pnpm@10.4.1 --activate
```

Tạo một database trống, ví dụ `novatickets`. Lúc khởi động, ứng dụng tự chạy migration Entity Framework Core.

Đặt biến môi trường trên máy chạy. Không commit mật khẩu, `DATABASE_URL` hay `JWT_SECRET` vào git.

MySQL trên máy, không bật SSL:

```bash
export ASPNETCORE_ENVIRONMENT=Development
export PORT=3000
export DATABASE_URL="Server=127.0.0.1;Port=3306;Database=novatickets;User ID=root;Password=YOUR_PASSWORD;SslMode=None;GuidFormat=Binary16"
export JWT_SECRET="chuoi-bi-mat-dai-it-nhat-32-ky-tu"
export ADMIN_PASSWORD="Admin@12345"
```

TiDB hoặc MySQL có SSL dùng dạng URL. Chuỗi này luôn bật SSL:

```bash
export DATABASE_URL="mysql://USER:PASSWORD@HOST:4000/DATABASE"
```

`JWT_SECRET` là bắt buộc khi `ASPNETCORE_ENVIRONMENT=Production`. Ở Development, nếu bỏ trống, ứng dụng dùng khóa tạm chỉ dành cho máy dev. `ADMIN_PASSWORD` chỉ được dùng ở lần seed đầu tiên, khi database chưa có user.

```bash
pnpm install
pnpm run dev
```

`pnpm run dev` build React vào `src/NovaTickets.Api/wwwroot` rồi chạy API. Mở http://localhost:3000. Swagger chỉ có khi môi trường là Development: http://localhost:3000/swagger.

Tài khoản tạo sẵn ở lần chạy đầu:

| Vai trò | Email | Mật khẩu |
| --- | --- | --- |
| Admin | admin@novatickets.vn | giá trị `ADMIN_PASSWORD`, mặc định `Admin@12345` |
| Khách | demo@novatickets.vn | `Demo@12345` |

Đổi hai mật khẩu này trước khi chạy ở môi trường dùng chung.

## Kiểm tra chất lượng

```bash
pnpm test
pnpm run build
bash scripts/test-seat-concurrency.sh
bash scripts/test-booking-flow.sh
```

Ứng dụng dùng Dockerfile đa giai đoạn để build React và chạy artifact ASP.NET Core trên runtime .NET 8.
