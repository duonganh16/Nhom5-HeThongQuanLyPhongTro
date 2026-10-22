namespace RoomRentalManagement.Models
{
    public class Invoice
    {
        public int InvoiceID { get; set; }

        public int ContractID { get; set; }

        public DateTime BillingMonth { get; set; }

        public decimal ElectricOld { get; set; }

        public decimal ElectricNew { get; set; }

        public decimal ElectricPrice { get; set; }

        public decimal WaterOld { get; set; }

        public decimal WaterNew { get; set; }

        public decimal WaterPrice { get; set; }

        public decimal RentAmount { get; set; }

        public decimal ServiceFee { get; set; }

        public decimal Total { get; set; }

        public string Status { get; set; } = "Unpaid";

        public Contract? Contract { get; set; }
    }
}