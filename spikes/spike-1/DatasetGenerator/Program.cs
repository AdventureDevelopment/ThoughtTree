using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DatasetGenerator;
using SpikeCore;

// Spike 1 dataset generator (docs/spikes/spike-1-storage-and-aot.md section 2). Test data only.
const string GeneratorVersion = "1.0.0";
const string SeedLabel = "thoughttree-spike-1-reference-v1";
const string AccountLabel = "spike-1-account";

var argsList = args.ToList();
string Opt(string name, string fallback) { int i = argsList.IndexOf(name); return i >= 0 && i + 1 < argsList.Count ? argsList[i + 1] : fallback; }
var command = args.Length > 0 ? args[0] : "generate";
var outDir = Path.GetFullPath(Opt("--out", Path.Combine(AppContext.BaseDirectory, "../../../../out")));
Directory.CreateDirectory(outDir);

ulong seed = SplitMix64.SeedFromLabel(SeedLabel);
uint idPrefix = (uint)(seed >> 32);
var key = EnvelopeFormat.TestKey(SeedLabel);
string keyId = "k" + Convert.ToHexStringLower(SHA256.HashData(key))[..7];   // short id (ADR-015), derived for test data
string accountId = new EventLogBuilder(new SplitMix64(SplitMix64.SeedFromLabel(AccountLabel)), idPrefix).NewId(7);

switch (command)
{
    case "generate":
    {
        var sizes = Opt("--sizes", "1000,10000,100000").Split(',').Select(int.Parse).ToArray();
        var manifestPath = Opt("--manifest", "");
        var datasets = new JsonArray();
        foreach (var n in sizes) datasets.Add(Generate(n, $"events-{n}", b => b.Build(n)));
        var longNotes = new JsonArray();
        foreach (var (label, chars) in new[] { ("10k", 10_000), ("50k", 50_000), ("200k", 200_000), ("1m", 1_000_000) })
            if (!argsList.Contains("--no-longnotes")) longNotes.Add(Generate(1000, $"longnote-{label}", b => b.BuildLongNotes(chars)));
        var manifest = new JsonObject
        {
            ["generatorVersion"] = GeneratorVersion,
            ["seedLabel"] = SeedLabel,
            ["seed"] = seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["random"] = "SplitMix64 (spikes/spike-1/SpikeCore/SplitMix64.cs); ids from per-kind counters; fake clock from 2027-01-01T00:00:00Z",
            ["format"] = new JsonObject
            {
                ["envelope"] = "one JSON object per line: v (envelope version 1), eventId, keyId, nonce (base64, 12 bytes), payload (base64 AES-GCM ciphertext with 16-byte tag); no ServerSequence (not synced)",
                ["payloadWrapper"] = "{\"v\":1,\"event\":{type, schema, deviceId, actionId, clock{time,counter}, timestamp, reverts?, data}} (ADR-073; optional parts would be siblings of event)",
                ["aad"] = "UTF-8 of contextLabel, envelope version, keyId, eventId, accountId joined by U+001F (ADR-073)",
                ["contextLabel"] = EnvelopeFormat.ContextLabel,
                ["padding"] = "payload wrapper UTF-8, then 0x80, then zeros to the bucket size; above the largest bucket a multiple of it",
                ["buckets"] = new JsonArray(EnvelopeFormat.ProvisionalBuckets.Select(b => (JsonNode)b).ToArray()),
                ["key"] = "SHA-256(\"thoughttree-spike-1 test key|\" + seedLabel); test data only",
                ["nonce"] = "big-endian 0x54545331 then the 64-bit event counter; test data only, never production (ADR-073)",
                ["keyId"] = keyId,
                ["accountId"] = accountId,
                ["eventTypes"] = "current format: relation ids (ADR-083), tag items in the Tags branch (ADR-082), relation type items (ADR-084), inbox (ADR-066)",
            },
            ["datasets"] = datasets,
            ["longNoteDatasets"] = longNotes,
        };
        var json = manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        File.WriteAllText(Path.Combine(outDir, "manifest.json"), json + "\n");
        if (manifestPath.Length > 0) File.WriteAllText(manifestPath, json + "\n");
        Console.WriteLine(json);
        break;
    }
    case "sizes":
        File.WriteAllText(Opt("--result", Path.Combine(outDir, "q5-sizes.json")), SizeAnalysis.Run(seed, idPrefix, Opt("--commit", "unknown")));
        break;
    case "verify":
    {
        var file = Opt("--file", Path.Combine(outDir, "events-100000.jsonl"));
        var p = Replay(file, out int count);
        Console.WriteLine($"{count} events, {p.Items.Count} items, checksum {p.Checksum()}");
        break;
    }
    default:
        Console.Error.WriteLine("usage: generate [--sizes 1000,10000,100000] [--out dir] [--manifest path] | verify --file f");
        return 1;
}
return 0;

JsonObject Generate(int n, string name, Func<EventLogBuilder, Dictionary<string, int>> build)
{
    var rng = new SplitMix64(seed);
    var b = new EventLogBuilder(rng, idPrefix);
    var counts = build(b);
    var snapshotAt = n - n / 100;
    var file = Path.Combine(outDir, $"{name}.jsonl");
    var plainLengths = new List<int>(n);
    var bucketHist = new SortedDictionary<int, int>();
    long paddedTotal = 0;
    var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    using (var aes = new AesGcm(key, 16))
    using (var fs = File.Create(file))
    {
        ulong counter = 0;
        foreach (var (eventId, ev) in b.Events)
        {
            counter++;
            var plain = JsonSerializer.SerializeToUtf8Bytes(new PayloadWrapper { V = 1, Event = ev }, SpikeJson.Relaxed.PayloadWrapper);
            plainLengths.Add(plain.Length);
            var padded = EnvelopeFormat.Pad(plain, EnvelopeFormat.ProvisionalBuckets);
            paddedTotal += padded.Length;
            bucketHist[padded.Length] = bucketHist.GetValueOrDefault(padded.Length) + 1;
            var nonce = EnvelopeFormat.TestNonce(0x54545331, counter);
            var ct = new byte[padded.Length + 16];
            aes.Encrypt(nonce, padded, ct.AsSpan(0, padded.Length), ct.AsSpan(padded.Length), EnvelopeFormat.EventAad(1, keyId, eventId, accountId));
            var env = new Envelope { V = 1, EventId = eventId, KeyId = keyId, Nonce = Convert.ToBase64String(nonce), Payload = Convert.ToBase64String(ct) };
            var line = JsonSerializer.SerializeToUtf8Bytes(env, SpikeJson.Relaxed.Envelope);
            fs.Write(line); fs.WriteByte((byte)'\n');
            sha.AppendData(line); sha.AppendData("\n"u8);
        }
    }
    var fileSha = Convert.ToHexStringLower(sha.GetHashAndReset());

    // Expected projections, computed by replaying the plaintext (the browser compares its checksum).
    var atSnapshot = new Projection();
    for (int i = 0; i < snapshotAt; i++) atSnapshot.Apply(b.Events[i].Event);
    plainLengths.Sort();
    int nItems = b.Shadow.Items.Count;
    var mix = counts.ToDictionary(kv => kv.Key, kv => Math.Round(100.0 * kv.Value / n, 2));
    Console.Error.WriteLine($"generated {file}: {n} events, {nItems} items, sha256 {fileSha}");
    return new JsonObject
    {
        ["name"] = name,
        ["file"] = Path.GetFileName(file),
        ["events"] = n,
        ["items"] = nItems,
        ["bytes"] = new FileInfo(file).Length,
        ["sha256"] = fileSha,
        ["eventsByType"] = new JsonObject(counts.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
        ["mixPercent"] = new JsonObject(mix.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
        ["payloadWrapperBytes"] = new JsonObject
        {
            ["min"] = plainLengths[0], ["median"] = plainLengths[plainLengths.Count / 2], ["p95"] = plainLengths[(int)(plainLengths.Count * 0.95)],
            ["max"] = plainLengths[^1], ["total"] = plainLengths.Sum(x => (long)x),
        },
        ["paddedBytesTotal"] = paddedTotal,
        ["paddedSizeHistogram"] = new JsonObject(bucketHist.Select(kv => KeyValuePair.Create(kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), (JsonNode?)kv.Value))),
        ["snapshotAfterEvent"] = snapshotAt,
        ["checksumAtSnapshot"] = atSnapshot.Checksum(),
        ["checksumFinal"] = b.Shadow.Checksum(),
        ["visibleItemsFinal"] = b.Shadow.VisibleCount(),
    };
}

Projection Replay(string file, out int count)
{
    var p = new Projection();
    count = 0;
    using var aes = new AesGcm(key, 16);
    foreach (var line in File.ReadLines(file))
    {
        var env = JsonSerializer.Deserialize(line, SpikeJson.Relaxed.Envelope)!;
        var nonce = Convert.FromBase64String(env.Nonce);
        var ct = Convert.FromBase64String(env.Payload);
        var padded = new byte[ct.Length - 16];
        aes.Decrypt(nonce, ct.AsSpan(0, padded.Length), ct.AsSpan(padded.Length), padded, EnvelopeFormat.EventAad(env.V, env.KeyId, env.EventId, accountId));
        var w = JsonSerializer.Deserialize(padded.AsSpan(0, EnvelopeFormat.UnpaddedLength(padded)), SpikeJson.Relaxed.PayloadWrapper)!;
        p.Apply(w.Event);
        count++;
    }
    return p;
}
