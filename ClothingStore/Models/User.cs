namespace ClothingStore.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Login { get; set; }
        public string PasswordHash { get; set; }
        public string Phone {  get; set; }
        public string DeliveryAddress { get; set; }
        public int RoleId { get; set; }
    }
}
