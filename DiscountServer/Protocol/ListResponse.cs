namespace DiscountServer.Protocol
{
    public class ListResponse : BaseMessage
    {
        public bool Result { get; set; }
        public List<string>? Codes { get; set; }
    }
}
