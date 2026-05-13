using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace FightNet.Shared;

public static class NetworkHelper
{
    // ── send ──────────────────────────────────────────────────────────────────

    public static async Task SendAsync<T>(NetworkStream stream, T message, CancellationToken ct = default)
        where T : BaseMessage
    {
        string json = JsonSerializer.Serialize(message, message.GetType());
        byte[] body = Encoding.UTF8.GetBytes(json);
        byte[] header = BitConverter.GetBytes(body.Length);

        await stream.WriteAsync(header, ct);
        await stream.WriteAsync(body, ct);
    }

    // ── receive ───────────────────────────────────────────────────────────────

    public static async Task<string?> ReceiveAsync(NetworkStream stream, CancellationToken ct = default)
    {
        byte[] header = new byte[4];
        if (!await ReadExactAsync(stream, header, 4, ct))
            return null;

        int length = BitConverter.ToInt32(header, 0);

        if (length <= 0 || length > GameConstants.MaxPacketBytes)
        {
            Console.WriteLine($"[NETWORK] Bad packet length: {length}");
            return null;
        }

        byte[] body = new byte[length];
        if (!await ReadExactAsync(stream, body, length, ct))
            return null;

        return Encoding.UTF8.GetString(body);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int total = 0;
        while (total < count)
        {
            int read = await stream.ReadAsync(buffer, total, count - total, ct);
            if (read == 0) return false;
            total += read;
        }
        return true;
    }
}
