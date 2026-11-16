# Đề tài 20 - Hệ thống thư viện và đặt mượn sách phân tán

## 1. Mô tả đề tài

Xây dựng hệ thống thư viện cho phép người dùng tra cứu sách, gửi yêu cầu đặt mượn và theo dõi trạng thái xử lý. Yêu cầu đặt mượn được xử lý bất đồng bộ để kiểm tra số bản còn khả dụng và xác nhận việc giữ sách.

Kiến trúc tối thiểu của hệ thống:

```text
Client -> Library API -> RabbitMQ -> Reservation Worker -> Database
             |                              |
             +----------> Database <--------+
```

## 2. Chức năng chính

1. Tra cứu danh sách và chi tiết sách.
2. Tra cứu số bản đang khả dụng.
3. Gửi yêu cầu đặt mượn sách.
4. Đưa yêu cầu vào RabbitMQ để xử lý.
5. Worker kiểm tra số bản khả dụng và xác nhận hoặc từ chối yêu cầu.
6. Cập nhật trạng thái `Pending`, `Reserved`, `Rejected`, `Borrowed`, `Returned`.
7. Tra cứu trạng thái yêu cầu đặt mượn.
8. Tra cứu danh sách sách đang mượn hoặc đã đặt của một người dùng.

## 3. API tối thiểu

```text
GET  /books
GET  /books/{id}
POST /reservations
GET  /reservations/{id}
GET  /users/{userId}/reservations
```

Dữ liệu tối thiểu gồm sách, số bản, người dùng, yêu cầu đặt mượn và trạng thái xử lý.
