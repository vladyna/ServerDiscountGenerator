namespace DiscountServer.Protocol
{
    public class GenerateRequest : BaseMessage
    {
        public ushort Count { get; set; }
        public byte Length { get; set; }
    }
}
