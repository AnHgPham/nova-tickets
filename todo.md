# Project TODO

- [x] Chốt đầy đủ yêu cầu chức năng, giao diện và phạm vi dự án với người dùng
- [x] Xây dựng website bán vé sự kiện ca nhạc bằng .NET
- [x] Thiết kế CRUD API đầy đủ cho sự kiện, địa điểm, suất diễn, khu vực, ghế, loại vé, đơn đặt vé và người dùng
- [x] Làm nổi bật luồng booking và giao diện chọn ghế
- [x] Xử lý an toàn trường hợp hai người cùng chọn một ghế tại cùng một thời điểm
- [x] Bổ sung xác thực, phân quyền, validation và lockout tài khoản
- [x] Viết kiểm thử chức năng, API và kiểm thử đồng thời cho booking ghế
- [x] Viết tài liệu API, hướng dẫn chạy dự án và playbook kiểm thử
- [x] Xây dựng frontend khách hàng đầy đủ, không dùng nút giả cho nghiệp vụ chính
- [x] Xây dựng trang quản trị cho các nghiệp vụ CRUD
- [x] Áp dụng transaction, unique constraint và idempotency để ngăn bán trùng ghế
- [x] Xây dựng cơ chế giữ ghế có thời hạn và tự giải phóng khi hết hạn
- [x] Bổ sung payment nội bộ demo và idempotency xác nhận payment
- [x] Chuẩn hóa phản hồi lỗi, global exception handling, audit log, rate limit và security headers
- [x] Bổ sung request logging có correlation ID
- [x] Kích hoạt và xác minh lịch cleanup hold ghế trên môi trường xuất bản
- [x] Bổ sung báo cáo phân tích và tài liệu giải pháp kỹ thuật theo form tham chiếu
- [x] Sửa lỗi nhãn “Quay lại sự kiện” hiển thị ngược
- [x] Bổ sung Email Outbox offline cho demo: render email, trạng thái Sent/Failed và retry
- [x] Kiểm thử Email Outbox tạo đúng một lần cùng booking — integration suite đã pass sau khi khôi phục code email
- [x] Kiểm tra API preview Email Outbox và retry bằng integration flow admin
- [x] Gửi source ZIP đầy đủ gồm backend, frontend, migrations, SQL và tài liệu database
- [x] Kiểm tra ZIP không chứa secrets, .env, node_modules, bin, obj hoặc output build
- [x] Xác minh phạm vi source hiện tại sau rollback và ghi rõ các phần email demo nếu chưa nằm trong checkpoint
- [x] Lưu checkpoint source cuối cùng sau khi hoàn tất gói bàn giao

## Database packaging scope

- [x] Liệt kê các migration SQL: 001_initial_novatickets_plain.sql, 002_add_catalog_entities.sql, 003_add_payment_idempotency.sql, 004_add_scheduler_setting.sql, 005_add_email_outbox.sql
- [x] Ghi rõ database engine, thứ tự migration và cảnh báo không chạy DROP/seed tùy tiện trên production
- [x] Đóng gói migration C# Entity Framework và SQL plain đã rà soát
- [x] Bổ sung hướng dẫn chạy backend .NET và áp dụng SQL an toàn
- [x] Đảm bảo package không chứa mật khẩu, API key hoặc secret runtime
- [x] Bàn giao package source cuối cùng

## Email demo note

- [x] Email Outbox offline lưu bản ghi trong database và hiển thị nội dung email demo
- [x] Ghi rõ trong package rằng email offline chưa gửi ra Gmail/Outlook thật
- [x] Ghi rõ payment hiện là payment nội bộ mô phỏng, chưa nối cổng thanh toán thật

## Current packaging request

- [x] Repackage full source code including SQL used by the system
- [x] Include database usage guide
- [x] Verify archive contents
- [x] Deliver final archive

## History note

- [x] Các hạng mục backend, booking, scheduler, frontend, tài liệu và kiểm thử trước đây đã được checkpoint
- [x] Email/auth thay đổi sau checkpoint cần được xác minh lại trước khi đưa vào gói source cuối cùng

## End

- [ ] Final source handoff
- [x] Xử lý OperationCanceledException đúng cách và tối ưu truy vấn /api/events để không ghi lỗi 500 giả trong preview
- [x] Kiểm tra EmailOutboxController và UI/API để xác nhận preview email và retry được expose
- [x] Bổ sung hoặc chạy integration test cho preview Email Outbox và retry bản ghi failed
- [x] Kiểm thử đầy đủ chuyển EmailOutbox Failed -> Sent qua retry admin với bản ghi failed thực sự
