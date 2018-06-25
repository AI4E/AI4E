using System;

namespace AI4E.Storage.Sample.Models
{
    public sealed class ProductListModel
    {
        public Guid Id { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
    }
}
