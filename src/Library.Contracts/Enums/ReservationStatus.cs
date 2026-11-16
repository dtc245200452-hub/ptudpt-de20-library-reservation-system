namespace Library.Contracts.Enums;

public enum ReservationStatus
{
    Pending = 0,    // Đang chờ Worker xử lý
    Reserved = 1,   // Đặt thành công, đã giữ sách
    Rejected = 2,   // Bị từ chối (hết sách)
    Borrowed = 3,   // Đã nhận/mượn sách
    Returned = 4    // Đã hoàn trả sách
}
