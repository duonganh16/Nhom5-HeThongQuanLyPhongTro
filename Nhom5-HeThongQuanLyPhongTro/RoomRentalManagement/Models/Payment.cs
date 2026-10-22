namespace RoomRentalManagement.Models
{
    public class Payment
    {
        public int PaymentID { get; set; }

        public int InvoiceID { get; set; }

        public DateTime PaymentDate { get; set; }

        public decimal Amount { get; set; }

        public string PaymentMethod { get; set; } = string.Empty;

        public string? Note { get; set; }

        public Invoice? Invoice { get; set; }
    }
}