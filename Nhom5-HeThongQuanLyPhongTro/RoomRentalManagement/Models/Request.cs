namespace RoomRentalManagement.Models
{
    public class Request
    {
        public int RequestID { get; set; }

        public int TenantID { get; set; }

        public string Content { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }

        public string Status { get; set; } = "Pending";

        public string? Response { get; set; }

        public Tenant? Tenant { get; set; }
    }
}