using DiscountClient.Models;
using DiscountClient.Services;

class Program
{
    private static TcpClientService _tcp;

    static async Task Main()
    {
        Console.WriteLine("Discount Client starting...");
        _tcp = new TcpClientService();
        await _tcp.ConnectAsync("localhost", 5000);
        Console.WriteLine("Connected.");
        var codes = await InitialLoadAsync();
        await InteractionLoopAsync(codes);
        await _tcp.DisposeAsync();
    }

    private static async Task<List<string>> InitialLoadAsync()
    {
        await _tcp.SendAsync("{\"Type\":\"List\",\"Limit\":20}");
        var line = await _tcp.ReceiveAsync();
        Console.WriteLine("List raw: " + (line ?? "<null>"));
        var list = _tcp.Deserialize<ListResponse>(line);
        if (list?.Codes?.Count > 0)
        {
            PrintCodes(list.Codes);
            return list.Codes;
        }
        Console.WriteLine("No existing codes; generating new ones.");
        await _tcp.SendAsync("{\"Type\":\"Generate\",\"Count\":5,\"Length\":8}");
        var genLine = await _tcp.ReceiveAsync();
        Console.WriteLine("Generate raw: " + (genLine ?? "<null>"));
        var gen = _tcp.Deserialize<GenerateResponse>(genLine);
        var codes = gen?.Codes ?? new List<string>();
        PrintCodes(codes);
        return codes;
    }

    private static void PrintCodes(List<string> codes)
    {
        if (codes.Count == 0)
        {
            Console.WriteLine("(no codes)");
            return;
        }
        Console.WriteLine("Codes:");
        for (int i = 0; i < codes.Count; i++)
        {
            Console.WriteLine($"  [{i}] {codes[i]}");
        }
    }

    private static async Task InteractionLoopAsync(List<string> codes)
    {
        while (true)
        {
            Console.Write("Enter code or index (list / gen / exit): ");
            var input = Console.ReadLine();
            if (input == null || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;
            if (input.Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                await _tcp.SendAsync("{\"Type\":\"List\",\"Limit\":20}");
                var l2 = await _tcp.ReceiveAsync();
                Console.WriteLine("List raw: " + (l2 ?? "<null>"));
                var listResp = _tcp.Deserialize<ListResponse>(l2);
                codes = listResp?.Codes ?? new List<string>();
                PrintCodes(codes);
                continue;
            }
            if (input.Equals("gen", StringComparison.OrdinalIgnoreCase))
            {
                await _tcp.SendAsync("{\"Type\":\"Generate\",\"Count\":5,\"Length\":8}");
                var g2 = await _tcp.ReceiveAsync();
                Console.WriteLine("Generate raw: " + (g2 ?? "<null>"));
                var genResp = _tcp.Deserialize<GenerateResponse>(g2);
                codes = genResp?.Codes ?? new List<string>();
                PrintCodes(codes);
                continue;
            }
            var code = ResolveCode(input, codes);
            if (code == null)
                continue;
            await _tcp.SendAsync($"{{\"Type\":\"Use\",\"Code\":\"{code}\"}}");
            var useLine = await _tcp.ReceiveAsync();
            Console.WriteLine("Use raw: " + (useLine ?? "<null>"));
            var useResp = _tcp.Deserialize<UseResponse>(useLine);
            if (useResp != null)
            {
                Console.WriteLine("Result: " + useResp.Result);
            }
        }
    }

    private static string? ResolveCode(string input, List<string> codes)
    {
        if (int.TryParse(input, out var idx))
        {
            if (idx >= 0 && idx < codes.Count)
                return codes[idx];
            return null;
        }
        if (string.IsNullOrWhiteSpace(input))
            return null;
        return input;
    }
}