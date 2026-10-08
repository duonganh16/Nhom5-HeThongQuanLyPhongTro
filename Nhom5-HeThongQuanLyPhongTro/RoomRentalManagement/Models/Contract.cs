namespace RoomRentalManagement.Models
{
    public class Contract
    {
        public int ContractID { get; set; }

        public int RoomID { get; set; }

        public int TenantID { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public decimal Deposit { get; set; }

        public decimal RentPrice { get; set; }

        public string Status { get; set; } = "Active";

        public Room? Room { get; set; }

        public Tenant? Tenant { get; set; }
    }
}