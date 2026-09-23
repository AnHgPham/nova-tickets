# NOVA Tickets API

NOVA Tickets là hệ thống bán vé sự kiện ca nhạc với backend **ASP.NET Core 8**, Entity Framework Core, MySQL/TiDB và frontend React. API trả về JSON theo camelCase, lỗi nghiệp vụ theo `application/problem+json`, có `code` và `traceId` để tra cứu log.

## Bảo mật và quy ước

| Hạng mục | Cách triển khai |
|---|---|
| Xác thực | JWT trong cookie `HttpOnly`, `SameSite=Strict`; frontend gửi request cùng origin |
| Phân quyền | `Customer`, `Staff`, `Admin`; API quản trị yêu cầu role phù hợp |
| Bảo vệ API | Rate limiting, security headers, validation model và global exception middleware |
| Theo dõi | `X-Correlation-ID` được nhận hoặc tự sinh; request logging và `AuditLogs` ghi các thao tác nhạy cảm |
| Dữ liệu ảnh | `GET /manus-storage/{key}` lấy URL có chữ ký từ storage proxy |

## Xác thực

| Method | Route | Quyền | Mục đích |
|---|---|---|---|
| `POST` | `/api/auth/register` | Public | Tạo tài khoản khách hàng |
| `POST` | `/api/auth/login` | Public | Đăng nhập và phát hành cookie JWT |
| `POST` | `/api/auth/logout` | Public | Xóa cookie phiên |
| `GET` | `/api/auth/me` | Authenticated | Đọc hồ sơ phiên hiện tại |

## CRUD quản trị

| Resource | API |
|---|---|
| Sự kiện | `GET/POST /api/events`, `GET/PUT/DELETE /api/events/{id}` |
| Địa điểm | `GET/POST /api/venues`, `GET/PUT/DELETE /api/venues/{id}` |
| Suất diễn | `GET /api/performances?eventId=`, `POST /api/performances`, `PUT/DELETE /api/performances/{id}` |
| Khu vực ghế | `GET /api/seat-areas`, `GET/POST/PUT/DELETE /api/seat-areas/{id}` |
| Ghế | `GET /api/seats?venueId=`, `GET/POST/PUT/DELETE /api/seats/{id}` |
| Loại vé | `GET /api/ticket-types`, `GET/POST/PUT/DELETE /api/ticket-types/{id}` |
| Giá theo suất diễn | `GET/POST/PUT /api/performances/{performanceId}/ticket-types`, `GET/DELETE /api/performances/{performanceId}/ticket-types/{ticketTypeId}` |
| Người dùng | `GET/POST /api/admin/users`, `GET/PUT/DELETE /api/admin/users/{id}`; xóa là **vô hiệu hóa** để bảo toàn lịch sử |
| Booking | `GET /api/bookings/mine`, `GET /api/bookings/{id}`, `GET /api/admin/bookings`; `PUT/DELETE /api/admin/bookings/{id}` áp dụng quy tắc trạng thái an toàn |

> Booking không có endpoint tạo thô của quản trị vì phải đi qua luồng giữ ghế và idempotency để bảo toàn toàn vẹn dữ liệu.

## Luồng giữ ghế và chống đặt trùng

1. Khách hàng đọc sơ đồ ghế qua `GET /api/performances/{performanceId}/seats`. Frontend refresh mỗi 4 giây; SignalR hub `SeatHub` cũng phát sự kiện trạng thái khi client kết nối.
2. Khách hàng gọi `POST /api/seat-holds` với `performanceId` và `seatIds`. Server kiểm tra cửa sổ bán vé, giới hạn ghế, và cập nhật từng hàng `SeatInventory` trong transaction.
3. Mỗi `UPDATE` chỉ thành công khi ghế còn `Available`, hoặc `Held` đã quá hạn. Nếu hai request chạm cùng ghế, request cập nhật hàng đầu tiên thành công; request còn lại nhận **409 `seat_unavailable`**.
4. Transaction chạy bên trong Entity Framework execution strategy tương thích MySQL retry. Hold có token riêng và hết hạn sau 7 phút. `DELETE /api/seat-holds/{holdToken}` giải phóng hold sớm.
5. `POST /api/bookings` nhận `holdToken` và `Idempotency-Key` trong body. Backend dùng transaction `ReadCommitted` tương thích TiDB, chỉ chấp nhận hàng ghế đang được chính user giữ và chưa hết hạn, rồi chuyển `Held → Sold`, phát hành `Booking`, `Payment` và `Ticket` trong cùng một commit.
6. Khóa duy nhất `(PerformanceId, SeatId)` của bảng `Tickets`, khóa idempotency `(UserId, IdempotencyKey)` của booking, khóa `(BookingId, IdempotencyKey)` của payment, và audit trail là các lớp bảo vệ bổ sung.

## Tự giải phóng ghế hết hạn

Mỗi lần tải sơ đồ ghế, backend chủ động dọn các hold hết hạn trước khi trả dữ liệu. Khi website đã được xuất bản, một tác vụ nền được bảo vệ bằng secret sẽ gọi `POST /api/scheduled/release-expired-holds` theo chu kỳ một phút để trạng thái ghế luôn được giải phóng ngay cả khi không có người mở sơ đồ ghế. Endpoint từ chối request thiếu khóa bảo mật và chỉ trả JSON trạng thái cho tác vụ hợp lệ.

## Kiểm thử đồng thời

Chạy lệnh dưới đây khi preview hoặc API đang lắng nghe tại cổng 3000:

```bash
bash scripts/test-seat-concurrency.sh
```

Kỳ vọng: một request nhận `200`, request còn lại nhận `409`; script luôn giải phóng hold chiến thắng sau kiểm thử.

## Chạy cục bộ

```bash
pnpm run dev
```

Ứng dụng build frontend vào `src/NovaTickets.Api/wwwroot` rồi chạy ASP.NET Core. Biến `DATABASE_URL`, `JWT_SECRET`, `BUILT_IN_FORGE_API_URL` và `BUILT_IN_FORGE_API_KEY` do môi trường quản lý cung cấp; không tạo hoặc commit `.env`.
