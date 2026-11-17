namespace ClothingStore.Models
{
    public class Order

    {
        public int Id { get; set; }
        public int StatusId { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<OrderItem> Items { get; set; }
        public User User { get; set; } = null!;

    }
}
