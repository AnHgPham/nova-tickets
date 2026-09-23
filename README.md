# NOVA Tickets

Website bán vé sự kiện ca nhạc full-stack bằng **ASP.NET Core 8** và **React**. Xem chi tiết endpoint, bảo mật và luồng chống trùng ghế trong [API.md](./API.md); kiến trúc dữ liệu trong [ARCHITECTURE.md](./ARCHITECTURE.md).

## Kiểm tra chất lượng

```bash
pnpm test
pnpm run build
bash scripts/test-seat-concurrency.sh
bash scripts/test-booking-flow.sh
```

Ứng dụng dùng Dockerfile đa giai đoạn để build React và chạy artifact ASP.NET Core trên runtime .NET 8.
