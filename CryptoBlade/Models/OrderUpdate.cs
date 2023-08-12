namespace CryptoBlade.Models
{
    public class OrderUpdate
    {
        public string Symbol { get; set; } = string.Empty;

        public OrderStatus Status { get; set; }

        public string OrderId { get; set; } = string.Empty;
    }
}