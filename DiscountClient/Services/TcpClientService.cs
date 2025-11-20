using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DiscountClient.Models;

namespace DiscountClient.Services
{
    public class TcpClientService : IAsyncDisposable
    {
        private readonly TcpClient _client = new();
        private NetworkStream _stream;
        private StreamWriter _writer;
        private StreamReader _reader;
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
        private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public async Task ConnectAsync(string host, int port)
        {
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();
            _writer = new StreamWriter(_stream, Utf8NoBom) { AutoFlush = true };
            _reader = new StreamReader(_stream, Utf8NoBom);
        }

        public async Task SendAsync(string json) => await _writer.WriteLineAsync(json);
        public async Task<string?> ReceiveAsync() => await _reader.ReadLineAsync();

        public T? Deserialize<T>(string? json) where T : class
        {
            if (json == null) return null;
            try { return JsonSerializer.Deserialize<T>(json, Opts); } catch { return null; }
        }

        public async ValueTask DisposeAsync()
        {
            _writer?.Dispose();
            _reader?.Dispose();
            _stream?.Dispose();
            _client.Dispose();
            await Task.CompletedTask;
        }
    }
}
