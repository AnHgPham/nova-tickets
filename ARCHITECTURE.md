# Kiến trúc NovaTickets

NovaTickets sử dụng **ASP.NET Core 8 Web API**, Entity Framework Core và MySQL/TiDB. Frontend React được biên dịch vào `wwwroot` và do chính ASP.NET Core phục vụ, nhờ đó frontend và API chạy cùng origin. API dùng JWT, phân quyền theo vai trò `Customer`, `Staff`, `Admin`, rate limiting, validation, global exception handling, audit log và security headers.

| Thành phần | Trách nhiệm |
| --- | --- |
| ASP.NET Core Web API | REST API, xác thực, phân quyền, booking và quản trị |
| Entity Framework Core | Truy cập MySQL/TiDB, transaction và ràng buộc dữ liệu |
| SignalR | Phát thay đổi trạng thái ghế theo từng suất diễn |
| React + TypeScript | Trang khách hàng, sơ đồ ghế, tài khoản và trang quản trị |
| MySQL/TiDB | Nguồn dữ liệu duy nhất cho ghế, giữ ghế, booking và vé |

## Cơ chế chống đặt trùng ghế

Mỗi ghế của một suất diễn có đúng một bản ghi `SeatInventory` với khóa chính `(PerformanceId, SeatId)`. Khi giữ ghế, API mở transaction và thực hiện cập nhật có điều kiện: bản ghi chỉ được chuyển sang `Held` nếu đang `Available` hoặc thời gian giữ cũ đã hết hạn. Kết quả cập nhật bằng `0` đồng nghĩa ghế đã bị người khác giữ hoặc bán; toàn bộ transaction bị rollback. Vì điều kiện được kiểm tra và cập nhật trong một câu lệnh tại cơ sở dữ liệu, hai request đồng thời không thể cùng giữ thành công một ghế.

Khi xác nhận booking, API kiểm tra `HoldToken`, chủ sở hữu và hạn giữ trong transaction, tạo booking/ticket rồi chuyển ghế sang `Sold`. Ràng buộc duy nhất `(PerformanceId, SeatId)` ở bảng `Ticket` là lớp bảo vệ cuối cùng. `IdempotencyKey` ngăn một request xác nhận bị gửi lại tạo ra hai booking.

Ghế hết hạn được coi là khả dụng ngay trong truy vấn và câu lệnh giữ ghế, nên tính đúng đắn không phụ thuộc vào tiến trình nền. Một tác vụ dọn dữ liệu có thể bổ sung sau để tối ưu, nhưng không tham gia quyết định ai sở hữu ghế.

