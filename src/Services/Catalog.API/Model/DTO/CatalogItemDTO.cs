using System.ComponentModel.DataAnnotations;

namespace Catalog.API.Model.DTO
{
    public class CatalogItemDTO
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; }
        public string Description { get; set; }
        [Required]
        public decimal Price { get; set; }
        [Required]
        public string CatalogType { get; set; }
        public string CatalogBrand { get; set; }
        public int AvailableStock { get; set; }
        public int RestockThreshold { get; set; }
        public int MaxStockThreshold { get; set; }
        public bool OnReorder { get; set; }

        public CatalogItemDTO()
        {
        }

        public CatalogItemDTO(string name, string description, decimal price, string catalogType, string catalogBrand, int availableStock, int restockThreshold, int maxStockThreshold, bool onReorder)
        {
            Name = name;
            Description = description;
            Price = price;
            CatalogType = catalogType;
            CatalogBrand = catalogBrand;
            AvailableStock = availableStock;
            RestockThreshold = restockThreshold;
            MaxStockThreshold = maxStockThreshold;
            OnReorder = onReorder;
        }
    }
}
