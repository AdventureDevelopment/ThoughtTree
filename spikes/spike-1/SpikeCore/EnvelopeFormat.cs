using System.Buffers.Binary;
using System.Text;

namespace SpikeCore;

/// <summary>
/// Spike choices for the bytes of envelope version 1 (ADR-015, ADR-073). Not a format decision: the real
/// layout belongs to Contracts and its format data.
/// </summary>
public static class EnvelopeFormat
{
    public const int Version = 1;
    public const string ContextLabel = "thoughttree/event/v1";
    public const string SnapshotContextLabel = "thoughttree/snapshot/v1";
    public const string IndexContextLabel = "thoughttree/search-index/v1";
    public const string DraftContextLabel = "thoughttree/draft/v1";

    /// <summary>Provisional bucket list of the plan: 256 B, 1 KiB, 4 KiB, 16 KiB, then multiples of 16 KiB.</summary>
    public static readonly int[] ProvisionalBuckets = [256, 1024, 4096, 16384];

    /// <summary>
    /// Additional authenticated data: every plaintext field except nonce and <c>ServerSequence</c>
    /// (envelope version, <c>KeyId</c>, <c>EventId</c>) plus account id and context label (ADR-073).
    /// Encoding (spike): UTF-8 of the fields joined by U+001F.
    /// </summary>
    public static byte[] EventAad(int version, string keyId, string eventId, string accountId) =>
        Encoding.UTF8.GetBytes(string.Join('\u001f', ContextLabel, version.ToString(System.Globalization.CultureInfo.InvariantCulture), keyId, eventId, accountId));

    /// <summary>AAD for local blobs (snapshot, index chunk, draft): label, key id, account id and the blob's own name.</summary>
    public static byte[] BlobAad(string label, string keyId, string accountId, string name) =>
        Encoding.UTF8.GetBytes(string.Join('\u001f', label, keyId, accountId, name));

    /// <summary>Padded size: smallest bucket that holds <paramref name="length"/>+1 (marker byte); above the largest, a multiple of it.</summary>
    public static int PaddedSize(int length, IReadOnlyList<int> buckets)
    {
        int need = length + 1;
        foreach (var b in buckets) if (need <= b) return b;
        int largest = buckets[^1];
        return (need + largest - 1) / largest * largest;
    }

    /// <summary>Pads with 0x80 then zeros (ISO/IEC 7816-4 style), so the original length is recoverable.</summary>
    public static byte[] Pad(ReadOnlySpan<byte> plain, IReadOnlyList<int> buckets)
    {
        var padded = new byte[PaddedSize(plain.Length, buckets)];
        plain.CopyTo(padded);
        padded[plain.Length] = 0x80;
        return padded;
    }

    public static int UnpaddedLength(ReadOnlySpan<byte> padded)
    {
        int i = padded.Length - 1;
        while (i >= 0 && padded[i] == 0) i--;
        if (i < 0 || padded[i] != 0x80) throw new FormatException("bad padding");
        return i;
    }

    /// <summary>Test-data nonce from the event counter (never production; production nonces are random, ADR-073).</summary>
    public static byte[] TestNonce(uint domain, ulong counter)
    {
        var n = new byte[12];
        BinaryPrimitives.WriteUInt32BigEndian(n, domain);
        BinaryPrimitives.WriteUInt64BigEndian(n.AsSpan(4), counter);
        return n;
    }

    public static byte[] TestKey(string seedLabel) =>
        System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("thoughttree-spike-1 test key|" + seedLabel));
}
