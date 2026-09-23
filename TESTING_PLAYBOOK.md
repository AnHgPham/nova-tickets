# NOVA Tickets — Luồng Kiểm Thử Chuẩn

Tài liệu này quy định cách kiểm thử hệ thống bán vé sự kiện ca nhạc **NOVA Tickets** trước khi nghiệm thu hoặc đưa phiên bản mới vào vận hành. Phạm vi gồm storefront khách hàng, quản trị viên, REST API ASP.NET Core, bảo mật, transaction booking, payment nội bộ và cơ chế tự giải phóng ghế.

> **Nguyên tắc nghiệm thu:** Không chỉ kiểm tra giao diện. Mỗi chức năng phải được xác nhận ở cả hành vi người dùng, phản hồi API, dữ liệu nghiệp vụ và quyền truy cập tương ứng.

## 1. Chuẩn bị kiểm thử

| Hạng mục | Giá trị / yêu cầu | Mục đích |
|---|---|---|
| Website | Mở URL môi trường cần kiểm thử | Kiểm tra storefront, API và console quản trị cùng origin |
| Tài khoản quản trị | `admin@novatickets.vn` | Kiểm thử CRUD vận hành, event, venue, suất diễn, ghế, giá vé, người dùng và booking |
| Tài khoản khách hàng | `demo@novatickets.vn` | Kiểm thử hành trình mua vé và chặn truy cập quản trị |
| Dữ liệu seed | Ít nhất một event đang mở bán, một suất diễn và ghế `Available` | Bảo đảm có dữ liệu để chạy booking, hold và xung đột ghế |
| Công cụ API | Swagger `/swagger` hoặc REST client | Đối chiếu payload, status code, body lỗi và quyền truy cập |
| Log / audit | Dashboard và nhật ký request/audit của ứng dụng | Truy vết bằng `traceId` hoặc `X-Correlation-ID` khi có lỗi |

## 2. Luồng smoke test bắt buộc

Thực hiện theo thứ tự dưới đây ngay sau khi triển khai. Một bước **FAIL** thì dừng nghiệm thu và ghi nhận `traceId`, request payload, user role và thời điểm xảy ra lỗi.

| Mã | Luồng kiểm thử | Thao tác | Kỳ vọng đạt |
|---|---|---|---|
| SMK-01 | Website công khai | Mở trang chủ, xem danh sách event và ảnh hero | HTTP 200; không có lỗi console; nội dung và ảnh hiển thị đúng trên desktop/mobile |
| SMK-02 | Đăng nhập | Đăng nhập bằng tài khoản khách hàng | HTTP 200; cookie `HttpOnly` được tạo; trang hiển thị trạng thái đăng nhập |
| SMK-03 | Phân quyền | Khi đang là khách hàng, mở `/api/admin/users` | HTTP 403; không lộ dữ liệu người dùng |
| SMK-04 | Sơ đồ ghế | Mở một suất diễn đang bán | Ghế `Available`, `Held`, `Sold` được thể hiện rõ; ghế đã giữ hiển thị thời hạn hợp lệ |
| SMK-05 | Giữ ghế | Chọn một ghế trống và bấm tiếp tục | API tạo hold token, ghế đổi sang `Held`, thời gian giữ là 7 phút |
| SMK-06 | Huỷ giữ ghế | Rời checkout hoặc xóa hold | API trả 204; ghế trở lại `Available` trên sơ đồ |
| SMK-07 | Booking | Giữ ghế, điền thông tin hợp lệ và xác nhận | Sinh một booking, ticket và payment nội bộ; ghế chuyển `Sold` |
| SMK-08 | Đơn vé | Mở “Vé của tôi” | Đơn có mã booking, event, suất diễn, venue, ghế, giá và mã ticket |
| SMK-09 | Quản trị | Đăng nhập admin và mở console quản trị | CRUD event, venue, performance, seat area, seat, ticket type, giá suất diễn, user và booking khả dụng theo quyền |

## 3. Kiểm thử booking và chống bán trùng ghế

Đây là luồng quan trọng nhất của hệ thống. Không được thay thế bằng chỉ kiểm tra màu ghế ở giao diện.

### 3.1 Luồng thành công

1. Khách hàng A đăng nhập và mở sơ đồ ghế của một suất diễn đang `OnSale`.
2. A chọn một hoặc nhiều ghế `Available`; gửi `POST /api/seat-holds`.
3. Xác nhận response có `holdToken`, `expiresAtUtc`, đúng `seatIds` và tổng tiền.
4. A gửi `POST /api/bookings` với `holdToken`, thông tin khách hàng và `idempotencyKey` mới.
5. Kiểm tra response có cùng mã booking/ticket; ghế đã mua đổi thành `Sold`.
6. Kiểm tra payment nội bộ được phát hành **một lần** với cùng idempotency key của booking.

| Điểm kiểm tra | Pass | Fail cần ghi nhận |
|---|---|---|
| Hold | HTTP 200, token riêng, ghế `Held` | 500, token rỗng, ghế không đổi trạng thái |
| Confirm | HTTP 200, booking/ticket/payment tồn tại | Lỗi transaction, tạo đơn không có ticket/payment |
| Retry confirm | Cùng `idempotencyKey` trả về cùng booking | Sinh booking/payment thứ hai |
| Seat state | Sau confirm là `Sold` | Ghế quay về `Available` hoặc vẫn `Held` |

### 3.2 Hai người chọn cùng một ghế

1. Mở hai phiên trình duyệt, dùng hai tài khoản khác nhau hoặc hai REST client.
2. Chuẩn bị hai request `POST /api/seat-holds` **cùng performanceId và seatId**.
3. Gửi đồng thời hai request trong cùng một thời điểm.
4. Xác nhận **chính xác một request nhận HTTP 200**, request còn lại nhận HTTP 409 với `code: seat_unavailable`.
5. Giải phóng hold thắng cuộc và tải lại seat map; ghế phải trở về `Available`.

> Tiêu chí pass là “một 200 và một 409”; hai 200 là lỗi nghiêm trọng vì có nguy cơ bán trùng, còn hai 409 thường cho thấy dữ liệu test đã không còn ghế trống hoặc hold trước chưa được dọn.

### 3.3 Hold hết hạn

1. Tạo hold nhưng không confirm booking.
2. Chờ quá 7 phút hoặc chạy test môi trường Testing.
3. Tải lại sơ đồ ghế hoặc chờ tác vụ cleanup chạy.
4. Xác nhận ghế trở về `Available`, `HoldToken` và `HeldByUserId` được xóa.
5. Một khách hàng khác phải giữ được ghế đó bình thường.

## 4. Kiểm thử API, validation và bảo mật

| Mã | Request / thao tác | Kỳ vọng HTTP | Kỳ vọng nghiệp vụ |
|---|---|---:|---|
| API-01 | `POST /api/auth/login` sai mật khẩu | 401 | Không phát cookie hợp lệ; lỗi không tiết lộ password hash |
| API-02 | `POST /api/seat-holds` với `seatIds: []` | 400 | Response có mã lỗi validation/nghiệp vụ rõ ràng |
| API-03 | `POST /api/seat-holds` khi suất diễn không mở bán | 409 | `sales_closed`; không tạo hold |
| API-04 | `POST /api/bookings` với hold của user khác/hết hạn | 409 | `hold_expired` hoặc lỗi nghiệp vụ tương đương; không phát vé |
| API-05 | Gọi resource quản trị bằng khách hàng | 403 | Không trả danh sách user hoặc dữ liệu vận hành |
| API-06 | Gọi API không tồn tại | 404 JSON | Không trả HTML SPA thay cho lỗi API |
| API-07 | Gửi quá nhanh request booking/login | 429 | Rate limit được áp dụng, hệ thống không lỗi 500 |
| API-08 | Request có `X-Correlation-ID` | 2xx/4xx theo nghiệp vụ | Có thể dùng ID này để tìm log request/audit tương ứng |
| API-09 | Kiểm tra response headers | 200 | Có `X-Content-Type-Options`, `X-Frame-Options`, CSP, Referrer-Policy và Permissions-Policy |

## 5. Kiểm thử CRUD quản trị

Quản trị viên thao tác trong **Admin Operations** hoặc Swagger cùng origin. Mỗi test CRUD phải xác minh cả response API lẫn ảnh hưởng ở storefront.

| Resource | Create / Update | Delete / trạng thái an toàn | Kiểm tra chéo storefront |
|---|---|---|---|
| Event | Tạo Draft, cập nhật title/artist/status | Không xóa khi đã có ràng buộc booking | Event `OnSale` hiển thị đúng, Draft không bán |
| Venue | Tạo/cập nhật tên, thành phố, địa chỉ | Chặn xóa khi đã có seat/performance liên quan | Venue hiển thị đúng trên chi tiết suất diễn |
| Performance | Tạo với event, venue, lịch và giá Standard/VIP | Chặn đổi venue nếu ghế đã bán/giữ | Lịch mở bán và sơ đồ ghế khớp dữ liệu |
| Seat area / Seat | Tạo khu vực, ghế, tọa độ, tier | Chặn xóa seat đã mở bán; dùng disable nếu cần | Seat map phản ánh section/row/number/tier |
| Ticket type / giá suất diễn | Tạo loại vé, giá, capacity | Xóa theo ràng buộc dữ liệu | Tổng tiền checkout đúng tier/giá |
| User | Tạo staff, đổi role, vô hiệu hóa | Không xoá cứng lịch sử | User bị vô hiệu hóa không tiếp tục đăng nhập |
| Booking | Cập nhật thông tin liên hệ/trạng thái theo quyền | Booking Paid không hủy thẳng; yêu cầu quy trình hoàn tiền | Vé của khách vẫn phản ánh trạng thái đúng |

## 6. Lệnh kiểm thử tự động

Chạy từ thư mục gốc dự án:

```bash
pnpm test
```

Lệnh trên chạy Vitest frontend và integration suite ASP.NET Core với SQLite cô lập. Dữ liệu test được tạo tạm thời và tự xóa sau khi hoàn tất; **không làm thay đổi dữ liệu production**.

| Kịch bản automation | Nội dung được xác minh |
|---|---|
| `scripts/test-seat-concurrency.sh` | Hai request giữ cùng ghế: một 200, một 409; sau release ghế về `Available` |
| `scripts/test-expired-hold-cleanup.sh` | Hold hết hạn được cleanup chủ động |
| `scripts/test-booking-flow.sh` | Hold → confirm → ticket → payment; retry cùng idempotency key trả về cùng booking |
| `scripts/test-integration.sh` | Khởi động backend SQLite cô lập và chạy các luồng booking, cleanup, auth, role, validation |

## 7. Kiểm thử sau triển khai production

Sau mỗi lần xuất bản, hãy chạy smoke test tại tên miền production và theo dõi tác vụ `nova-release-expired-seat-holds` trong phần lịch chạy của dự án. Tác vụ phải gọi `POST /api/scheduled/release-expired-holds` mỗi phút và trả response 2xx. Nếu tác vụ thất bại, đối chiếu thời điểm chạy với request log và `traceId`.

| Hạng mục | Tần suất | Tiêu chí đạt |
|---|---|---|
| Trang chủ + ảnh | Mỗi deployment | HTTP 200, ảnh tải được, responsive desktop/mobile |
| Đăng nhập + role | Mỗi deployment | Customer bị chặn resource admin; Admin vận hành được CRUD |
| Booking happy path | Mỗi release nghiệp vụ | Booking, ticket, payment cùng được phát hành |
| Xung đột ghế | Mỗi release nghiệp vụ | Một 200 / một 409; không bán trùng |
| Job cleanup | Sau deployment và hàng ngày | Job enabled, có execution log 2xx, hold quá hạn được giải phóng |

## 8. Mẫu biên bản nghiệm thu

| Ngày / giờ | Môi trường | Build / checkpoint | Người kiểm thử | Kết quả | Trace ID hoặc ghi chú |
|---|---|---|---|---|---|
|  | Staging / Production |  |  | Pass / Fail |  |

Chỉ xác nhận nghiệm thu khi tất cả case bắt buộc đạt, không còn lỗi 500 chưa phân tích, và không có dấu hiệu tạo trùng booking, ticket hoặc payment.
