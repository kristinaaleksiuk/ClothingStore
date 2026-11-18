using System;
using System.Collections.Generic;

namespace ClothingStore.Models;

public partial class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal Price { get; set; }

    public string? Description { get; set; }

    public int PurchaseCount { get; set; }

    public int ClothingTypeId { get; set; }

    public bool IsAvailable { get; set; }

    public virtual ClothingType ClothingType { get; set; } = null!;

    public virtual ICollection<ProductSize> ProductSizes { get; set; } = new List<ProductSize>();
}
