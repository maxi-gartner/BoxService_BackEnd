namespace BoxService_BackEnd.Models
{
    public class CatalogItem
    {
        public int     CatalogId { get; set; }
        public string  Name      { get; set; } = string.Empty;
        public string  Type      { get; set; } = "labor"; // labor | part
        public decimal Price     { get; set; }
    }

    public class CatalogItemCreateRequest
    {
        public string  Name  { get; set; } = string.Empty;
        public string  Type  { get; set; } = "labor";
        public decimal Price { get; set; }
    }

    public class CatalogItemUpdateRequest
    {
        public string?  Name  { get; set; }
        public string?  Type  { get; set; }
        public decimal? Price { get; set; }
    }
}
