namespace RoomRentalManagement.Models
{
    public class Tenant
    {
        public int TenantID { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public string CCCD { get; set; } = string.Empty;

        public DateTime? DateOfBirth { get; set; }

        public string? Address { get; set; }

        public int? UserID { get; set; }

        public User? User { get; set; }
    }
}