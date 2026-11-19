using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SpikeCore;

namespace DatasetGenerator;

/// <summary>
/// Q5 (plan section 3): stored bytes and overhead of bucket lists on the reference dataset, and the pairing code size
/// against QR code capacity (ADR-028). Pure computation over the generated events.
/// </summary>
public static class SizeAnalysis
{
    // QR code capacity in bytes (byte mode) and characters (alphanumeric mode), ISO/IEC 18004, error correction L and M.
    private static readonly (int Version, int ByteL, int ByteM, int AlnumL, int AlnumM)[] Qr =
    [
        (10, 271, 213, 395, 311), (15, 520, 412, 758, 600), (20, 858, 666, 1249, 970), (25, 1273, 997, 1853, 1451), (40, 2953, 2331, 4296, 3391),
    ];

    public static string Run(ulong seed, uint idPrefix, string commit)
    {
        var b = new EventLogBuilder(new SplitMix64(seed), idPrefix);
        b.Build(100_000);
        var lengths = new List<(string Type, int Len)>(b.Events.Count);
        foreach (var (_, e) in b.Events)
            lengths.Add((e.Type, JsonSerializer.SerializeToUtf8Bytes(new PayloadWrapper { Event = e }, SpikeJson.Relaxed.PayloadWrapper).Length));
        long unpadded = lengths.Sum(l => (long)l.Len);

        var lists = new (string Name, int[] Buckets)[]
        {
            ("provisional: 256 B, 1, 4, 16 KiB (factor 4)", [256, 1024, 4096, 16384]),
            ("factor 2: 256 B to 16 KiB (7 buckets)", [256, 512, 1024, 2048, 4096, 8192, 16384]),
            ("factor 2: 512 B to 16 KiB (6 buckets)", [512, 1024, 2048, 4096, 8192, 16384]),
            ("5 buckets: 512 B, 1, 4, 16, 64 KiB", [512, 1024, 4096, 16384, 65536]),
            ("8 buckets: 256 B to 32 KiB (factor 2)", [256, 512, 1024, 2048, 4096, 8192, 16384, 32768]),
            ("4 buckets: 512 B, 2, 8, 32 KiB (factor 4)", [512, 2048, 8192, 32768]),
        };
        var bucketResults = new JsonArray();
        foreach (var (name, buckets) in lists)
        {
            long padded = 0;
            var hist = new SortedDictionary<int, int>();
            foreach (var (_, len) in lengths) { int p = EnvelopeFormat.PaddedSize(len, buckets); padded += p; hist[p] = hist.GetValueOrDefault(p) + 1; }
            // Stored per envelope in IndexedDB: padded payload + 16-byte tag + 12-byte nonce + eventId (36) + keyId (8) + version and structure (~40, measured separately).
            bucketResults.Add(new JsonObject
            {
                ["list"] = name, ["buckets"] = new JsonArray(buckets.Select(x => (JsonNode)x).ToArray()),
                ["paddedBytes"] = padded, ["overheadPercent"] = Math.Round(100.0 * (padded - unpadded) / unpadded, 1),
                ["meanPaddedBytesPerEvent"] = Math.Round((double)padded / lengths.Count, 1),
                ["ciphertextBytes"] = padded + 16L * lengths.Count,
                ["base64FileBytesEstimate"] = (padded + 16L * lengths.Count) * 4 / 3 + 120L * lengths.Count,
                ["sizesUsed"] = hist.Count,
                ["histogram"] = new JsonObject(hist.Select(kv => KeyValuePair.Create(kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), (JsonNode?)kv.Value))),
            });
        }

        var byType = new JsonObject();
        foreach (var g in lengths.GroupBy(l => l.Type).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var v = g.Select(x => x.Len).Order().ToList();
            byType[g.Key] = new JsonObject { ["count"] = v.Count, ["min"] = v[0], ["median"] = v[v.Count / 2], ["p95"] = v[(int)(v.Count * 0.95)], ["max"] = v[^1] };
        }

        // Pairing code (ADR-028): format version, tree access (key 32, KeyId 8, account id 16, account secret 32, server
        // address), user identity (user id 16, name). Binary layout with one-byte length prefixes (two for the address).
        var pairing = new JsonArray();
        foreach (var trees in new[] { 1, 3, 10 })
        foreach (var nameChars in new[] { 30, 64, 100 })
        foreach (var addrChars in new[] { 40, 100, 200 })
        {
            int nameBytes = Encoding.UTF8.GetByteCount(new string('ü', nameChars / 4) + new string('a', nameChars - nameChars / 4)); // a quarter umlauts
            int perTree = 16 /*tree id*/ + 32 + 8 + 16 + 32 + 2 + addrChars;
            int binary = 1 /*version*/ + 1 /*kind*/ + 1 /*tree count*/ + trees * perTree + 16 + 1 + nameBytes;
            int base32Chars = (binary * 8 + 4) / 5;
            var fits = new JsonObject();
            foreach (var q in Qr)
                fits[$"v{q.Version}"] = new JsonObject { ["byteL"] = binary <= q.ByteL, ["byteM"] = binary <= q.ByteM, ["base32AlnumM"] = base32Chars <= q.AlnumM };
            pairing.Add(new JsonObject { ["trees"] = trees, ["nameChars"] = nameChars, ["nameBytes"] = nameBytes, ["addressChars"] = addrChars, ["binaryBytes"] = binary, ["base32Chars"] = base32Chars, ["fits"] = fits });
        }

        var result = new JsonObject
        {
            ["question"] = "q5", ["device"] = "n/a (computation over the reference dataset)", ["gitCommit"] = commit, ["seed"] = "thoughttree-spike-1-reference-v1",
            ["dataset"] = "events-100000", ["events"] = lengths.Count, ["payloadWrapperBytesTotal"] = unpadded,
            ["payloadWrapperBytesByType"] = byType,
            ["bucketLists"] = bucketResults,
            ["qrCapacity"] = new JsonArray(Qr.Select(q => (JsonNode)new JsonObject { ["version"] = q.Version, ["byteL"] = q.ByteL, ["byteM"] = q.ByteM, ["alphanumericL"] = q.AlnumL, ["alphanumericM"] = q.AlnumM }).ToArray()),
            ["pairingCodeLayout"] = "spike assumption (no format exists yet): version 1 B, code kind 1 B, tree count 1 B; per tree: tree id 16, key 32, KeyId 8, account id 16, account secret 32, address length 2 + UTF-8; user id 16, name length 1 + UTF-8 (a quarter of the name as umlauts)",
            ["pairingCode"] = pairing,
        };
        return result.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
    }
}
