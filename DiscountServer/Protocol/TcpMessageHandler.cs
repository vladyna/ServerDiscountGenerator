using DiscountServer.Data;
using DiscountServer.Services;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace DiscountServer.Protocol
{
    public class TcpMessageHandler
    {
        #region Fields
        private readonly DiscountRepository _repo;
        private readonly JsonSerializerOptions _json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private const int MaxMessageBytes = 16 * 1024;
        private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);
        private const int MaxGenerateAttempts = 5000;
        #endregion

        #region Ctor
        public TcpMessageHandler(DiscountRepository repo) => _repo = repo;
        #endregion

        #region Entry
        public async Task HandleClientAsync(TcpClient client)
        {
            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            while (true)
            {
                var line = await ReadLineAsync(stream);
                if (line == null)
                    break;

                var type = GetTypeName(line);

                if (type == null)
                    continue;

                Console.WriteLine($"[TCP] Received {type} request");

                if (type == "Generate")
                    await ProcessGenerate(line, writer);

                else if (type == "Use")
                    await ProcessUse(line, writer);

                else if (type == "List")
                    await ProcessList(line, writer);
            }
        }
        #endregion

        #region Framing
        private async Task<string?> ReadLineAsync(Stream stream)
        {
            var start = DateTime.UtcNow;
            var buffer = new byte[1];
            var ms = new MemoryStream();
            while (true)
            {
                if (DateTime.UtcNow - start > ReadTimeout)
                    return null;

                var read = await stream.ReadAsync(buffer, 0, 1);
                if (read == 0)
                    return null;

                var b = buffer[0];
                if (b == (byte)'\n')
                    break;

                if (ms.Length >= MaxMessageBytes)
                    return null;

                if (b != (byte)'\r')
                    ms.WriteByte(b);
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        #endregion

        #region Parsing
        private string? GetTypeName(string json)
        {
            try
            {
                var bm = JsonSerializer.Deserialize<BaseMessage>(json, _json);
                return string.IsNullOrWhiteSpace(bm?.Type) ? null : bm.Type;
            }
            catch { return null; }
        }

        private T? Deserialize<T>(string json) where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, _json);
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region Generate
        private async Task ProcessGenerate(string json, StreamWriter writer)
        {
            var req = Deserialize<GenerateRequest>(json);
            if (!ValidateGenerate(req))
            {
                Console.WriteLine("[Generate] Invalid request");
                await writer.WriteLineAsync(JsonSerializer.Serialize(new GenerateResponse { Type = "GenerateResponse", Result = false, Codes = new List<string>() }));
                return;
            }
            var codes = await GenerateCodes(req.Count, req.Length);
            var success = codes.Count == req.Count;
            Console.WriteLine($"[Generate] Produced {codes.Count}/{req.Count} codes");
            var resp = new GenerateResponse { Type = "GenerateResponse", Result = success, Codes = codes };
            await writer.WriteLineAsync(JsonSerializer.Serialize(resp));
        }

        private bool ValidateGenerate(GenerateRequest? req) => req != null && req.Count >= 1 && req.Count <= 2000 && req.Count <= ushort.MaxValue && req.Length >= 7 && req.Length <= 8;

        private async Task<List<string>> GenerateCodes(int count, int length)
        {
            var generated = new List<string>();
            int attempts = 0;
            int collisionsTotal = 0;
            while (generated.Count < count && attempts < MaxGenerateAttempts)
            {
                attempts++;
                var batch = BuildBatch(count - generated.Count, length);
                var inserted = (await _repo.InsertCodesAsync(batch, length)).ToList();
                generated.AddRange(inserted);

                collisionsTotal += batch.Count - inserted.Count;
                if (attempts % 10 == 0 && generated.Count == 0)
                    await Task.Delay(20);
            }
            return generated;
        }

        private HashSet<string> BuildBatch(int need, int length)
        {
            var size = Math.Min(need * 2, 500);
            var set = new HashSet<string>();

            for (int i = 0; i < size; i++)
                set.Add(CodeGenerator.RandomCode(length));
            return set;
        }
        #endregion

        #region List
        private async Task ProcessList(string json, StreamWriter writer)
        {
            var req = Deserialize<ListRequest>(json);
            int limit = 100;
            if (req?.Limit != null && req.Limit.Value > 0)
            {
                var requested = (int)req.Limit.Value;
                limit = requested > 1000 ? 1000 : requested;
            }
            int? lenFilter = req?.Length.HasValue == true ? (int)req.Length.Value : null;
            if (lenFilter.HasValue && (lenFilter < 7 || lenFilter > 8))
                lenFilter = null;
            var codes = await _repo.ListCodesAsync(lenFilter, limit);
            var resp = new ListResponse { Type = "ListResponse", Result = true, Codes = codes.ToList() };
            await writer.WriteLineAsync(JsonSerializer.Serialize(resp));
        }
        #endregion

        #region Use
        private async Task ProcessUse(string json, StreamWriter writer)
        {
            var req = Deserialize<UseRequest>(json);
            if (!ValidateUse(req))
            {
                Console.WriteLine("[Use] Invalid request");
                await writer.WriteLineAsync(JsonSerializer.Serialize(new UseResponse { Type = "UseResponse", Result = 3 }));
                return;
            }
            var codeResult = await _repo.UseCodeAsync(req!.Code);
            var result = codeResult switch { 0 => (byte)0, 1 => (byte)1, 2 => (byte)2, _ => (byte)3 };
            Console.WriteLine($"[Use] Code {req.Code} result {result}");
            await writer.WriteLineAsync(JsonSerializer.Serialize(new UseResponse { Type = "UseResponse", Result = result }));
        }

        private bool ValidateUse(UseRequest? req) => req != null && !string.IsNullOrWhiteSpace(req.Code) && req.Code.Length >= 7 && req.Code.Length <= 8;
        #endregion
    }
}
