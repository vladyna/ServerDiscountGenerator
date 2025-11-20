namespace DiscountServer.Protocol
{
    public class GenerateResponse : BaseMessage
    {
        public bool Result { get; set; }
        public List<string>? Codes { get; set; }
    }
}
