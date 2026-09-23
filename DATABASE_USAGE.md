# NOVA Tickets — Database Usage Guide

## 1. Database engine

NOVA Tickets sử dụng MySQL-compatible database/TiDB thông qua Entity Framework Core 8 và provider Pomelo MySQL. Các khóa định danh dạng GUID được lưu theo cấu hình tương thích với TiDB. Không lưu mật khẩu dạng rõ; mật khẩu được lưu dưới dạng hash trong backend.

## 2. Thứ tự migration SQL đã rà soát

Các file SQL plain trong `database/` được áp dụng theo thứ tự sau:

| Thứ tự | File | Nội dung |
|---:|---|---|
| 1 | `001_initial_novatickets_plain.sql` | Users, venues, events, performances, seats, bookings, tickets, payments và các khóa/ràng buộc ban đầu |
| 2 | `002_add_catalog_entities.sql` | Seat areas, ticket types và giá vé theo suất diễn |
| 3 | `003_add_payment_idempotency.sql` | Khóa idempotency cho payment và unique index liên quan |
| 4 | `004_add_scheduler_setting.sql` | Cấu hình hash scheduler để xác thực callback cleanup hold ghế |
| 5 | `005_add_email_outbox.sql` | Email Outbox offline: nội dung HTML/text, trạng thái gửi và retry |

Các migration C# tương ứng nằm tại `src/NovaTickets.Api/Data/Migrations/`. SQL plain được tạo để rà soát và áp dụng an toàn qua công cụ quản trị database; migration C# được giữ để Entity Framework theo dõi schema trong source.

## 3. Cách áp dụng an toàn

Trước khi chạy migration, cần kiểm tra database đang ở phiên bản nào và sao lưu dữ liệu production. Chỉ chạy các file còn thiếu theo đúng thứ tự; không chạy lặp lại file đã áp dụng nếu file không có cơ chế idempotent phù hợp. Không chạy `DROP TABLE`, không dùng seed demo trên production tùy tiện và không ghi đè bảng `users` mặc định của scaffold.

Ví dụ kiểm tra schema bằng MySQL/TiDB CLI:

```sql
SHOW TABLES;
SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId;
```

Ví dụ chạy migration plain trên database đã chọn:

```bash
mysql --host="$DB_HOST" --user="$DB_USER" --password --database="$DB_NAME" < database/001_initial_novatickets_plain.sql
mysql --host="$DB_HOST" --user="$DB_USER" --password --database="$DB_NAME" < database/002_add_catalog_entities.sql
mysql --host="$DB_HOST" --user="$DB_USER" --password --database="$DB_NAME" < database/003_add_payment_idempotency.sql
mysql --host="$DB_HOST" --user="$DB_USER" --password --database="$DB_NAME" < database/004_add_scheduler_setting.sql
mysql --host="$DB_HOST" --user="$DB_USER" --password --database="$DB_NAME" < database/005_add_email_outbox.sql
```

Không đưa giá trị thật của `DATABASE_URL`, JWT secret, scheduler key, SMTP credential hoặc API key vào source. Các giá trị này phải được cấu hình bằng secret/environment của môi trường chạy.

## 4. Các bảng nghiệp vụ chính

| Nhóm | Bảng tiêu biểu | Vai trò |
|---|---|---|
| Tài khoản | `AppUsers` | Người mua, quản trị viên, trạng thái lockout và quyền |
| Catalog | `Venues`, `Events`, `Performances`, `SeatAreas`, `Seats`, `TicketTypes`, `PerformancePrices` | Sự kiện, địa điểm, suất diễn, sơ đồ ghế và giá |
| Giao dịch | `Bookings`, `BookingSeats`, `Tickets`, `Payments` | Giữ ghế, xác nhận đơn, phát hành vé và payment nội bộ demo |
| Vận hành | `AuditLogs`, `SystemSettings`, `EmailOutbox` | Audit trail, scheduler hash, offline email preview và retry |


Booking phải được bảo vệ bằng transaction, thời hạn giữ ghế và unique constraint theo suất diễn/ghế. Không tin trạng thái ghế từ frontend; backend phải kiểm tra lại trước khi giữ hoặc xác nhận.

## 5. Phạm vi package hiện tại

Package source gồm backend ASP.NET Core, frontend React, migration C#, SQL plain, Dockerfile, scripts kiểm thử và tài liệu. Email Outbox offline được lưu trong database, render HTML/text, có preview cho khách hàng hoặc admin và retry qua API admin; hệ thống chưa gửi email ra Gmail/Outlook thật vì chưa cấu hình SMTP/Resend.
