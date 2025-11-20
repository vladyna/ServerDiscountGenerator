namespace DiscountClient.Models
{
    public class ListResponse
    {
        public string Type { get; set; }
        public bool Result { get; set; }
        public List<string>? Codes { get; set; }
    }
}
