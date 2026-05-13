using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace FightNet.Shared;

public static class NetworkHelper
{
    // ── send ──────────────────────────────────────────────────────────────────

    public static async Task SendAsync<T>(NetworkStream stream, T message) where T : BaseMessage
    {
        string json = JsonSerializer.Serialize(message, message.GetType());
        byte[] body = Encoding.UTF8.GetBytes(json);
        byte[] header = BitConverter.GetBytes(body.Length);

        await stream.WriteAsync(header);
        await stream.WriteAsync(body);
    }

    // ── receive ───────────────────────────────────────────────────────────────

    public static async Task<string?> ReceiveAsync(NetworkStream stream)
    {
        // read the 4-byte length header
        byte[] header = new byte[4];
        if (!await ReadExactAsync(stream, header, 4))
            return null; // disconnected

        int length = BitConverter.ToInt32(header, 0);

        // sanity check — reject obviously bad packets
        if (length <= 0 || length > 1_048_576) // 1MB max
        {
            Console.WriteLine($"[NETWORK] Bad packet length: {length}");
            return null;
        }

        // read exactly `length` bytes
        byte[] body = new byte[length];
        if (!await ReadExactAsync(stream, body, length))
            return null;

        return Encoding.UTF8.GetString(body);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    // keeps reading until we have exactly `count` bytes
    // (TCP can split data into multiple chunks)
    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read = await stream.ReadAsync(buffer, total, count - total);
            if (read == 0) return false; // connection closed
            total += read;
        }
        return true;
    }
}