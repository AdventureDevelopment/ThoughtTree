using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SpikeCore;

namespace SpikeApp;

/// <summary>
/// Runs one measured scenario per page load (spike plan Q1, Q2, Q4, Q5) and reports a JSON result.
/// Query parameters: scenario (empty | setup | generate | snapshot | full | import | noteio), dataset, seedLabel, keyId, accountId,
/// checksum, snapshotChecksum, snapshotAt, chunk, batch, parallel, importBatch, durability, search (none | A | B | C).
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("browser")]
public sealed class Runner
{
    public Projection? Projection;
    public string Status = "starting";
    public int Progress;
    public readonly JsonObject Result = new();
    private readonly JsonObject _phases = new();
    private readonly List<(string Id, string OldTitle, string OldNote, string NewTitle, string NewNote)> _tailChanges = new();

    private static string Q(string name, string fallback) { var v = Js.Query(name); return v.Length == 0 ? fallback : v; }
    private static int QI(string name, int fallback) => int.TryParse(Js.Query(name), out var v) ? v : fallback;

    public static string BuildMode =>
        typeof(Runner).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "SpikeBuild")?.Value ?? "unknown";

    private void Phase(string name, double ms) => _phases[name] = Math.Round((_phases[name]?.GetValue<double>() ?? 0) + ms, 3);

    public async Task<double> RunAsync(Func<Task> render)
    {
        var scenario = Q("scenario", "empty");
        var dataset = Q("dataset", "events-100000");
        int chunk = QI("chunk", 5000), batch = QI("batch", 1000), importBatch = QI("importBatch", 1000);
        bool parallel = Q("parallel", "1") == "1";
        var durability = Q("durability", "default");
        var search = Q("search", "none");
        var db = "tt-spike-" + dataset;
        Result["scenario"] = scenario; Result["dataset"] = dataset; Result["build"] = BuildMode;
        Result["params"] = new JsonObject { ["chunk"] = chunk, ["batch"] = batch, ["parallel"] = parallel, ["importBatch"] = importBatch, ["durability"] = durability, ["search"] = search };

        double treeVisible = 0;
        if (scenario == "empty") { Status = "empty app ready"; return 0; }

        await Js.InitKey(Q("seedLabel", ""), Q("keyId", ""), Q("accountId", ""));
        switch (scenario)
        {
            case "setup":
                await Js.DeleteDb(db);
                await Js.OpenDb(db, durability);
                Status = "importing";
                await render();
                Result["imported"] = await Js.ImportFile("/data/" + dataset + ".jsonl", importBatch);
                Projection = new Projection();
                Status = "replaying and writing snapshot and index";
                await render();
                await ReplayFrom(0, chunk, batch, parallel, render, stopAt: QI("snapshotAt", 0));
                Result["checksumAtSnapshotOk"] = Projection.Checksum() == Q("snapshotChecksum", "");
                await WriteSnapshotAndIndexes();
                await ReplayFrom(Js.LastSeq(), chunk, batch, parallel, render);
                break;
            case "generate":
            {
                // On-device setup without a computer: builds the dataset with the desktop generator's code, encrypts it
                // with Web Crypto (same test key and nonces), stores it and checks its SHA-256 against the manifest.
                await Js.DeleteDb(db);
                await Js.OpenDb(db, durability);
                Status = "generating";
                await render();
                await Generate(dataset, importBatch, render);
                Projection = new Projection();
                Status = "replaying and writing snapshot and index";
                await render();
                int stop = QI("snapshotAt", 0);
                await ReplayFrom(0, chunk, batch, parallel, render, stopAt: stop);
                Result["checksumAtSnapshotOk"] = Projection.Checksum() == Q("snapshotChecksum", "");
                if (stop > 0) await WriteSnapshotAndIndexes();
                await ReplayFrom(Js.LastSeq(), chunk, batch, parallel, render);
                break;
            }
            case "import":
                await Js.DeleteDb(db);
                await Js.OpenDb(db, durability);
                Status = "importing";
                await render();
                Result["imported"] = await Js.ImportFile("/data/" + dataset + ".jsonl", importBatch);
                Projection = new Projection();
                await ReplayFrom(0, chunk, batch, parallel, render);
                break;
            case "full":
                await Js.OpenDb(db, durability);
                Projection = new Projection();
                await ReplayFrom(0, chunk, batch, parallel, render);
                break;
            case "snapshot":
            {
                await Js.OpenDb(db, durability);
                int seq = await Js.GetSnapshot();
                if (seq == 0) throw new InvalidOperationException("no snapshot; run scenario=setup first");
                var t = Js.Now();
                var bytes = Js.TakeBuffer();
                Phase("snapshotCopyMs", Js.Now() - t);
                t = Js.Now();
                var dto = JsonSerializer.Deserialize(bytes, SpikeJson.Default.SnapshotDto)!;
                Projection = Projection.FromSnapshot(dto);
                Phase("snapshotParseMs", Js.Now() - t);
                Result["snapshotSeq"] = seq;
                Result["snapshotPlainBytes"] = bytes.Length;
                Projection.TextChanged = (id, ot, on, nt, nn) => _tailChanges.Add((id, ot, on, nt, nn));
                await ReplayFrom(seq, chunk, batch, parallel, render);
                Projection.TextChanged = null;
                break;
            }
            default:
                throw new InvalidOperationException("unknown scenario " + scenario);
        }

        Status = "tree visible";
        await render();
        Js.Mark("tree-visible");
        treeVisible = Js.Now();
        Result["treeVisibleMs"] = Math.Round(treeVisible, 3);

        // Correctness: the browser replay must give the generator's projection.
        Result["checksumOk"] = Projection!.Checksum() == Q("checksum", "");
        Result["items"] = Projection.Items.Count;
        Result["eventsApplied"] = Projection.EventsApplied;
        Result["memoryAfterTree"] = MemorySnapshot();

        if (search != "none") await RunSearch(search, treeVisible);
        if (scenario is "import" or "full" or "generate" && Q("noteio", "0") == "1") await NoteIo();
        return treeVisible;
    }

    private async Task Generate(string dataset, int batchSize, Func<Task> render)
    {
        var seed = SplitMix64.SeedFromLabel(Q("seedLabel", ""));
        var builder = new DatasetGenerator.EventLogBuilder(new SplitMix64(seed), (uint)(seed >> 32));
        var t = Js.Now();
        if (dataset.StartsWith("events-", StringComparison.Ordinal)) builder.Build(int.Parse(dataset["events-".Length..], System.Globalization.CultureInfo.InvariantCulture));
        else builder.BuildLongNotes(dataset switch { "longnote-10k" => 10_000, "longnote-50k" => 50_000, "longnote-200k" => 200_000, "longnote-1m" => 1_000_000, _ => throw new InvalidOperationException("unknown dataset " + dataset) });
        Phase("generateBuildMs", Js.Now() - t);

        Js.GenerateBegin();
        var events = builder.Events;
        for (int start = 0; start < events.Count; start += batchSize)
        {
            t = Js.Now();
            int end = Math.Min(events.Count, start + batchSize);
            var padded = new List<byte[]>(end - start);
            for (int i = start; i < end; i++)
                padded.Add(EnvelopeFormat.Pad(JsonSerializer.SerializeToUtf8Bytes(new PayloadWrapper { V = 1, Event = events[i].Event }, SpikeJson.Relaxed.PayloadWrapper), EnvelopeFormat.ProvisionalBuckets));
            var all = new byte[padded.Sum(p => p.Length)];
            int o = 0;
            foreach (var p in padded) { p.CopyTo(all, o); o += p.Length; }
            Phase("generateSerializePadMs", Js.Now() - t);
            Js.StageBytes(all);
            await Js.EncryptStoreBatch(
                string.Join('\n', events.Skip(start).Take(end - start).Select(e => e.EventId)),
                string.Join(',', padded.Select(p => p.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                start + 1);
            Progress = end;
            Status = $"generated {end} of {events.Count} events";
            await render();
        }
        var sha = Js.GenerateEnd();
        Result["generated"] = events.Count;
        Result["datasetSha256"] = sha;
        Result["datasetShaOk"] = sha == Q("sha256", "");
        Progress = 0;
    }

    private async Task ReplayFrom(int afterSeq, int chunk, int batch, bool parallel, Func<Task> render, int stopAt = 0)
    {
        int after = afterSeq;
        var events = new List<EventRecord>(chunk);
        while (true)
        {
            int want = stopAt > 0 ? Math.Min(chunk, stopAt - after) : chunk;
            if (want <= 0) break;
            int len = await Js.ReadDecrypt(after, want, batch, parallel);
            if (len == 0) break;
            var t = Js.Now();
            var buf = Js.TakeBuffer();
            Phase("copyMs", Js.Now() - t);
            t = Js.Now();
            events.Clear();
            int start = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                if (buf[i] != (byte)'\n') continue;
                events.Add(JsonSerializer.Deserialize(buf.AsSpan(start, i - start), SpikeJson.Default.PayloadWrapper)!.Event);
                start = i + 1;
            }
            Phase("parseMs", Js.Now() - t);
            t = Js.Now();
            foreach (var e in events) Projection!.Apply(e);
            Phase("applyMs", Js.Now() - t);
            after = Js.LastSeq();
            Progress += events.Count;
            Status = $"replayed {Progress} events";
            await render();
        }
    }

    private async Task WriteSnapshotAndIndexes()
    {
        var t = Js.Now();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(Projection!.ToSnapshot(Js.LastSeq()), SpikeJson.Relaxed.SnapshotDto);
        Phase("snapshotSerializeMs", Js.Now() - t);
        Js.StageBytes(bytes);
        await Js.PutSnapshot(Js.LastSeq());
        Result["snapshotPlainBytes"] = bytes.Length;

        t = Js.Now();
        var ix = SearchIndex.Build(Projection);
        Phase("indexBuildAtSnapshotMs", Js.Now() - t);
        t = Js.Now();
        var chunks = ix.ToChunks(2000).Select(c => JsonSerializer.SerializeToUtf8Bytes(c, SpikeJson.Relaxed.IndexChunkDto)).ToList();
        Phase("indexSerializeMs", Js.Now() - t);
        Result["indexTerms"] = ix.TermCount;
        Result["indexPostings"] = ix.PostingCount;
        Result["indexPlainBytes"] = chunks.Sum(c => (long)c.Length);
        Result["indexChunks"] = chunks.Count;
        foreach (var variant in new[] { "B", "C" })
        {
            foreach (var c in chunks) Js.AddIndexChunk(c);
            Result["indexStoredBytes" + variant] = await Js.FlushIndexChunks(variant);
        }
    }

    private async Task RunSearch(string variant, double treeVisible)
    {
        var p = Projection!;
        SearchIndex ix;
        var t0 = Js.Now();
        if (variant == "A")
        {
            ix = SearchIndex.Build(p);
            Phase("indexBuildMs", Js.Now() - t0);
        }
        else
        {
            int n = await Js.LoadIndexChunks(variant);
            var t = Js.Now();
            ix = new SearchIndex();
            for (int i = 0; i < n; i++)
            {
                var bytes = new byte[Js.IndexChunkLength(i)];
                Js.TakeIndexChunk(i, bytes);
                ix.Load(JsonSerializer.Deserialize(bytes, SpikeJson.Default.IndexChunkDto)!);
            }
            Phase("indexParseMs", Js.Now() - t);
            t = Js.Now();
            foreach (var c in _tailChanges) ix.Update(c.Id, c.OldTitle, c.OldNote, c.NewTitle, c.NewNote);
            Phase("indexTailUpdateMs", Js.Now() - t);
            Result["indexTailUpdates"] = _tailChanges.Count;
        }
        var queries = Queries(p);
        var tq = Js.Now();
        var first = ix.Query(queries[0].Text, p);
        var firstHits = Js.Now();
        Phase("firstQueryMs", firstHits - tq);
        Result["indexReadyMs"] = Math.Round(tq - t0, 3);
        Result["treeVisibleToFirstHitsMs"] = Math.Round(firstHits - treeVisible, 3);
        Result["startToFirstHitsMs"] = Math.Round(firstHits, 3);
        Result["firstQueryHits"] = first.Count;
        Result["indexTerms"] = ix.TermCount;

        var q = new JsonObject();
        foreach (var (kind, text) in queries)
        {
            var samples = new List<double>();
            int hits = 0;
            for (int r = 0; r < 20; r++) { var t = Js.Now(); hits = ix.Query(text, p).Count; samples.Add(Js.Now() - t); }
            samples.Sort();
            q[kind] = new JsonObject { ["query"] = text, ["hits"] = hits, ["medianMs"] = Math.Round(samples[10], 4), ["p95Ms"] = Math.Round(samples[18], 4), ["maxMs"] = Math.Round(samples[^1], 4) };
        }
        Result["queries"] = q;

        // Incremental update cost: 1,000 title changes (title of item i set to the title of item i+1).
        var items = p.Items.Values.Where(i => !i.Deleted).Take(1001).ToList();
        var tu = Js.Now();
        for (int i = 0; i < items.Count - 1; i++) ix.Update(items[i].Id, items[i].Title, items[i].Note, items[i + 1].Title, items[i].Note);
        Result["indexUpdatePerEventUs"] = Math.Round((Js.Now() - tu) * 1000 / Math.Max(1, items.Count - 1), 2);
        Result["memoryAfterSearch"] = MemorySnapshot();
    }

    /// <summary>Deterministic queries taken from the projection: 1, 2 and 3 words, a prefix, German equivalent and accent.</summary>
    private static List<(string Kind, string Text)> Queries(Projection p)
    {
        var visible = p.Items.Values.Where(i => !i.Deleted && i.Title.Length > 0).OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
        string Words(int idx, int n)
        {
            var tokens = new List<string>();
            Normaliser.Tokens(visible[idx % visible.Count].Title, tokens);
            return string.Join(' ', tokens.Where(t => t.Length > 1).Take(n));
        }
        var w4 = new List<string>();
        Normaliser.Tokens(visible[(visible.Count * 4) / 10].Title, w4);
        var prefixWord = w4.FirstOrDefault(w => w.Length >= 4) ?? "idee";
        return
        [
            ("oneWord", Words(visible.Count / 10, 1)),
            ("twoWords", Words(visible.Count / 5, 2)),
            ("threeWords", Words(visible.Count * 3 / 10, 3)),
            ("prefix", prefixWord[..3]),
            ("germanEquivalent", "baer"),
            ("accentFree", "cafe"),
        ];
    }

    /// <summary>Q5: time to save and load one long note event (encrypt and append; read, decrypt and parse).</summary>
    private async Task NoteIo()
    {
        var p = Projection!;
        var item = p.Items.Values.OrderByDescending(i => i.Note.Length).First();
        var saves = new List<double>(); var loads = new List<double>();
        for (int r = 0; r < 10; r++)
        {
            var e = new EventRecord { Type = "item.note-changed", DeviceId = "noteio", ActionId = "noteio-" + r, Clock = new Clock { Time = 1, Counter = r }, Timestamp = "2027-01-01T00:00:00.000+01:00", Data = new EventData { ItemId = item.Id, Note = item.Note, Format = "markdown", Replaces = [] } };
            var t = Js.Now();
            var plain = JsonSerializer.SerializeToUtf8Bytes(new PayloadWrapper { Event = e }, SpikeJson.Relaxed.PayloadWrapper);
            Js.StageBytes(EnvelopeFormat.Pad(plain, EnvelopeFormat.ProvisionalBuckets));
            int seq = await Js.EncryptAppend($"noteio-{r}");
            saves.Add(Js.Now() - t);
            t = Js.Now();
            await Js.ReadDecrypt(seq - 1, 1, 1, true);
            var buf = Js.TakeBuffer();
            var back = JsonSerializer.Deserialize(buf.AsSpan(0, buf.Length - 1), SpikeJson.Default.PayloadWrapper)!;
            loads.Add(Js.Now() - t);
            if (back.Event.Data.Note != item.Note) throw new InvalidOperationException("note round trip failed");
        }
        saves.Sort(); loads.Sort();
        Result["noteIo"] = new JsonObject { ["noteChars"] = item.Note.Length, ["saveMedianMs"] = Math.Round(saves[5], 3), ["loadMedianMs"] = Math.Round(loads[5], 3), ["saveMaxMs"] = Math.Round(saves[^1], 3), ["loadMaxMs"] = Math.Round(loads[^1], 3) };
    }

    private static JsonObject MemorySnapshot()
    {
        var m = JsonNode.Parse(Js.Memory())!.AsObject();
        m["dotnetManagedBytes"] = GC.GetTotalMemory(false);
        m["dotnetHeapSizeBytes"] = GC.GetGCMemoryInfo().HeapSizeBytes;
        return m;
    }

    public async Task<string> FinishAsync(double appReady, string? error)
    {
        Result["appReadyMs"] = Math.Round(appReady, 3);
        if (error != null) Result["error"] = error;
        Result["phases"] = _phases;
        Result["jsPhases"] = JsonNode.Parse(Js.Stats());
        try { Result["storage"] = JsonNode.Parse(await Js.StorageEstimate()); } catch { }
        Result["environment"] = JsonNode.Parse(Js.Environment());
        Result["dotnetVersion"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        var json = Result.ToJsonString();
        Js.Report(json);
        return json;
    }
}
