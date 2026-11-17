namespace ClothingStore.Models
{
    public class ProductSize
    {
        public int Id { get; set; }
        public int Stock {  get; set; }
        public Product Product { get; set; } = null!;
        public Size Size { get; set; } = null!;
    }
}
