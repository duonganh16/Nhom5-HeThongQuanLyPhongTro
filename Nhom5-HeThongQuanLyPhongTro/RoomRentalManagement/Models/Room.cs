
namespace RoomRentalManagement.Models
{
    public class Room
    {
        public int RoomID { get; set; }

        public string RoomName { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public decimal Area { get; set; }

        public string Status { get; set; } = "Available";

        public string? Description { get; set; }

        // Tài khoản chủ nhà sở hữu phòng
        public int? LandlordID { get; set; }

        public User? Landlord { get; set; }
    }
}
