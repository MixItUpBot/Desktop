using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace MixItUp.Base.Web;

internal sealed record DnsAnswer(string Name, string Type, string Value, uint Ttl);

internal sealed record DnsReply(int ResponseCode, bool Truncated, DnsAnswer[] Answers, string[] Addresses,
    string Transport = "udp", string UdpStatus = "response");

// A small bounded DNS wire reader lets us select the resolver's transport family independently
// of the A/AAAA question. Used only with existing configured resolvers after Windows stalls.
internal static class ConfiguredDnsQuery
{
    public static async Task<DnsReply> Query(string host, ushort type, IPAddress resolver, CancellationToken token,
        int port = 53, Action<string, string> transportChanged = null)
    {
        ushort id = (ushort)RandomNumberGenerator.GetInt32(65536);
        var query = BuildQuery(host, type, id);
        string udpStatus;
        using (var socket = new Socket(resolver.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
        {
            using var udpDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            udpDeadline.CancelAfter(TimeSpan.FromMilliseconds(1800));
            transportChanged?.Invoke("udp", "pending");
            try
            {
                await socket.ConnectAsync(new IPEndPoint(resolver, port), udpDeadline.Token);
                await socket.SendAsync(query, SocketFlags.None, udpDeadline.Token);
                var buffer = new byte[16384];
                int count = await socket.ReceiveAsync(buffer, SocketFlags.None, udpDeadline.Token);
                var reply = Parse(buffer.AsSpan(0, count), host, type, id);
                if (!reply.Truncated)
                {
                    return reply;
                }
                udpStatus = "truncated";
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                udpStatus = "timeout";
            }
            catch (SocketException)
            {
                udpStatus = "socket-error";
            }
        }
        token.ThrowIfCancellationRequested();
        transportChanged?.Invoke("tcp", udpStatus);
        using var tcp = new TcpClient(resolver.AddressFamily);
        await tcp.ConnectAsync(resolver, port, token);
        using var stream = tcp.GetStream();
        var prefix = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(prefix, (ushort)query.Length);
        await stream.WriteAsync(prefix, token);
        await stream.WriteAsync(query, token);
        await stream.ReadExactlyAsync(prefix, token);
        int length = BinaryPrimitives.ReadUInt16BigEndian(prefix);
        if (length < 12 || length > 16384)
        {
            throw new InvalidDataException("DNS response size");
        }
        var packet = new byte[length];
        await stream.ReadExactlyAsync(packet, token);
        var result = Parse(packet, host, type, id);
        if (result.Truncated)
        {
            throw new InvalidDataException("Truncated DNS TCP response");
        }
        return result with { Transport = "tcp", UdpStatus = udpStatus };
    }

    internal static byte[] BuildQuery(string host, ushort type, ushort id)
    {
        if (host.Length > 253 || (type != 1 && type != 28))
        {
            throw new ArgumentException("DNS question");
        }
        using var stream = new MemoryStream();
        var header = new byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(header, id);
        header[2] = 1;
        header[5] = 1;
        stream.Write(header);
        foreach (var label in host.TrimEnd('.').Split('.'))
        {
            if (label.Length is < 1 or > 63 || label.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            {
                throw new ArgumentException("DNS name");
            }
            stream.WriteByte((byte)label.Length);
            stream.Write(Encoding.ASCII.GetBytes(label));
        }
        stream.WriteByte(0);
        stream.WriteByte((byte)(type >> 8));
        stream.WriteByte((byte)type);
        stream.WriteByte(0);
        stream.WriteByte(1);
        return stream.ToArray();
    }

    internal static DnsReply Parse(ReadOnlySpan<byte> packet, string host, ushort type, ushort id)
    {
        if (packet.Length < 12 || U16(packet, 0) != id || (U16(packet, 2) & 0xf800) != 0x8000 || U16(packet, 4) != 1)
        {
            throw new InvalidDataException("DNS header mismatch");
        }
        int flags = U16(packet, 2);
        int position = 12;
        string question = ReadName(packet, ref position);
        if (!SameName(question, host) || U16(packet, position) != type || U16(packet, position + 2) != 1)
        {
            throw new InvalidDataException("DNS question mismatch");
        }
        position += 4;
        if ((flags & 0x200) != 0)
        {
            return new(flags & 15, true, [], []);
        }
        int answerCount = U16(packet, 6);
        int total = answerCount + U16(packet, 8) + U16(packet, 10);
        if (total > 256)
        {
            throw new InvalidDataException("DNS record limit");
        }
        var answers = new List<DnsAnswer>();
        for (int i = 0; i < total; i++)
        {
            string name = ReadName(packet, ref position);
            int recordType = U16(packet, position);
            int recordClass = U16(packet, position + 2);
            Require(packet, position, 10);
            uint ttl = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(position + 4, 4));
            int length = U16(packet, position + 8);
            position += 10;
            Require(packet, position, length);
            if (i < answerCount && recordClass == 1)
            {
                if (recordType == 1 && length == 4 || recordType == 28 && length == 16)
                {
                    answers.Add(new(name, recordType == 1 ? "A" : "AAAA", new IPAddress(packet.Slice(position, length)).ToString(), ttl));
                }
                else if (recordType == 5)
                {
                    int cnamePosition = position;
                    string alias = ReadName(packet, ref cnamePosition);
                    if (cnamePosition != position + length)
                    {
                        throw new InvalidDataException("DNS alias length");
                    }
                    answers.Add(new(name, "CNAME", alias, ttl));
                }
            }
            position += length;
        }
        // Only use answers belonging to the requested name or its bounded alias chain.
        var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { host.TrimEnd('.') };
        for (int i = 0; i < 16; i++)
        {
            var aliases = answers.Where(a => a.Type == "CNAME" && owners.Contains(a.Name)).Select(a => a.Value).ToArray();
            bool addedAlias = false;
            foreach (string alias in aliases)
            {
                if (owners.Add(alias))
                {
                    addedAlias = true;
                }
            }
            if (!addedAlias)
            {
                break;
            }
        }
        var relevant = answers.Where(a => owners.Contains(a.Name)).ToArray();
        string wanted = type == 1 ? "A" : "AAAA";
        return new(flags & 15, false, relevant, (flags & 15) == 0 ? relevant.Where(a => a.Type == wanted).Select(a => a.Value).Distinct().ToArray() : []);
    }

    private static string ReadName(ReadOnlySpan<byte> packet, ref int position)
    {
        int cursor = position;
        int end = -1;
        int hops = 0;
        var labels = new List<string>();
        while (true)
        {
            Require(packet, cursor, 1);
            if (++hops > 128)
            {
                throw new InvalidDataException("DNS name loop");
            }
            int length = packet[cursor++];
            if ((length & 0xc0) == 0xc0)
            {
                Require(packet, cursor, 1);
                if (end < 0)
                {
                    end = cursor + 1;
                }
                cursor = ((length & 63) << 8) | packet[cursor];
                continue;
            }
            if (length > 63)
            {
                throw new InvalidDataException("DNS label");
            }
            if (length == 0)
            {
                position = end < 0 ? cursor : end;
                break;
            }
            Require(packet, cursor, length);
            var label = Encoding.ASCII.GetString(packet.Slice(cursor, length));
            if (label.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            {
                throw new InvalidDataException("DNS label characters");
            }
            labels.Add(label);
            cursor += length;
            if (labels.Sum(l => l.Length + 1) > 254)
            {
                throw new InvalidDataException("DNS name length");
            }
        }
        return string.Join('.', labels);
    }
    private static bool SameName(string a, string b) => a.Equals(b.TrimEnd('.'), StringComparison.OrdinalIgnoreCase);
    private static ushort U16(ReadOnlySpan<byte> bytes, int offset)
    {
        Require(bytes, offset, 2);
        return BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
    }
    private static void Require(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("Short DNS packet");
        }
    }
}
