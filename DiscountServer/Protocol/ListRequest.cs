namespace DiscountServer.Protocol
{
    public class ListRequest : BaseMessage
    {
        public ushort? Limit { get; set; }
        public byte? Length { get; set; }
    }
}
