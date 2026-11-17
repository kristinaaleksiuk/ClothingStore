namespace ClothingStore.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Price { get; set; }
        public string Description { get; set; }
        public int PurchaseCount { get; set; }
        public int ClothingTypeId { get; set; }
        public int isAvailable { get; set; }
    }
}
