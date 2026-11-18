using System;
using System.Collections.Generic;

namespace ClothingStore.Models;

public partial class Size
{
    public int Id { get; set; }

    public string Label { get; set; } = null!;

    public int NumberSize { get; set; }

    public virtual ICollection<ProductSize> ProductSizes { get; set; } = new List<ProductSize>();
}
