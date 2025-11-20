namespace DiscountClient.Models
{
    public class GenerateResponse
    {
        public string Type { get; set; }
        public bool Result { get; set; }
        public List<string>? Codes { get; set; }
    }
}
