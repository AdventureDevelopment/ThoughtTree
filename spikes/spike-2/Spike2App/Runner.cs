using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SpikeCore;

namespace Spike2App;

/// <summary>
/// Runs one scenario of spike 2 per page load (docs/plan/next-work.md, "Spike 2: content blobs and replay speed")
/// and reports a JSON result. Scenarios: setup | replay1 | replay2 | replay3 | structure4 | erase | sync.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("browser")]
public sealed class Runner
{
    // Spike 1's reference dataset of 100,000 events (docs/spikes/spike-1/data/manifest.json); test data only.
    private const string SeedLabel = "thoughttree-spike-1-reference-v1";
    private const string KeyId = "k812292d";
    private const string AccountId = "080b131c-0007-4000-8000-000000000001";
    private const string TreeId = "080b131c-0008-4000-8000-000000000001";
    private const string ExpectedChecksum = "effc144400289563f807cb9d3afcbeba574fca375a4a4eb7a4cd0063c0f57a37";
    private const int Events = 100_000, BlockSize = 1000, ChunkBlocks = 10, VisibleRows = 50;
    private const string SetupVersion = "spike2-setup-1";

    private static readonly HashSet<string> TextTypes = new(StringComparer.Ordinal)
        { "item.created", "item.title-changed", "item.note-changed", "relation.note-changed", "relation-type.set", "device.joined" };

    public ContentProjection? Projection;
    public string Status = "starting";
    public int Progress;
    public readonly JsonObject Result = new();
    private readonly JsonObject _phases = new();
    private Func<Task> _render = () => Task.CompletedTask;

    public static string BuildMode =>
        typeof(Runner).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "SpikeBuild")?.Value ?? "unknown";

    private void Phase(string name, double ms) => _phases[name] = Math.Round((_phases[name]?.GetValue<double>() ?? 0) + ms, 3);
    private static double R(double x) => Math.Round(x, 3);

    public async Task RunAsync(Func<Task> render)
    {
        _render = render;
        var scenario = Js.Query("scenario");
        if (scenario.Length == 0) scenario = "replay1";
        Result["scenario"] = scenario;
        Result["build"] = BuildMode;
        await Js.InitKey(SeedLabel, KeyId, AccountId, TreeId);
        if (scenario == "setup") { await Setup(); return; }

        await Js.Open("L1,L2,L3");
        foreach (var layout in new[] { "L1", "L2", "L3" })
            if (await Js.GetMeta(layout, "setup") != "\"" + SetupVersion + "\"") throw new InvalidOperationException("test data missing: run scenario=setup first");

        switch (scenario)
        {
            case "replay1": await ReplayCase("L1", "blocks"); break;
            case "replay2": await ReplayCase("L2", "blocks"); break;
            case "replay3": await ReplayCase("L3", "blocks,cblocks"); break;
            case "structure4": await StructureFirst(); break;
            case "erase": await Erase(); break;
            case "sync": await Sync(); break;
            default: throw new InvalidOperationException("unknown scenario " + scenario);
        }
    }

    // ---- Setup: generate the dataset and write all three layouts ----

    private async Task Setup()
    {
        await Js.ResetAll();
        await Js.Open("L1,L2,L3");
        Status = "generating 100,000 events";
        await _render();
        var t = Js.Now();
        var seed = SplitMix64.SeedFromLabel(SeedLabel);
        var builder = new DatasetGenerator.EventLogBuilder(new SplitMix64(seed), (uint)(seed >> 32));
        builder.Build(Events);
        Phase("generateMs", Js.Now() - t);
        Result["generatorChecksumOk"] = builder.Shadow.Checksum() == ExpectedChecksum;

        var events = builder.Events;
        int blobs = 0;
        for (int block = 0; block * BlockSize < events.Count; block++)
        {
            t = Js.Now();
            var full = new ArrayBufferWriter<byte>(); var structure = new ArrayBufferWriter<byte>(); var content = new ArrayBufferWriter<byte>();
            var b1 = new ArrayBufferWriter<byte>(); var b2 = new ArrayBufferWriter<byte>(); var e3 = new ArrayBufferWriter<byte>(); var c3 = new ArrayBufferWriter<byte>();
            var ids = new List<string>(); var blobIds = new List<string>();
            var fullLens = new List<int>(); var structLens = new List<int>(); var contentLens = new List<int>();
            for (int i = block * BlockSize; i < Math.Min(events.Count, (block + 1) * BlockSize); i++)
            {
                var (eventId, e) = events[i];
                var (st, c) = ContentFormat.Split(e);
                ids.Add(eventId);
                fullLens.Add(Write(full, JsonSerializer.SerializeToUtf8Bytes(new PayloadWrapper { Event = e }, Spike2Json.Relaxed.PayloadWrapper)));
                structLens.Add(Write(structure, JsonSerializer.SerializeToUtf8Bytes(new PayloadWrapper { Event = st }, Spike2Json.Relaxed.PayloadWrapper)));
                Write(b1, ContentFormat.Line(new BlockLine { Id = eventId, Event = e }, Spike2Json.Relaxed.BlockLine));
                Write(b2, ContentFormat.Line(new BlockLine { Id = eventId, Event = st, Content = c }, Spike2Json.Relaxed.BlockLine));
                Write(e3, ContentFormat.Line(new BlockLine { Id = eventId, Event = st }, Spike2Json.Relaxed.BlockLine));
                if (c != null)
                {
                    var blobId = ContentFormat.BlobIdOf(eventId);
                    blobIds.Add(blobId);
                    contentLens.Add(Write(content, JsonSerializer.SerializeToUtf8Bytes(c, Spike2Json.Relaxed.Content)));
                    Write(c3, ContentFormat.Line(new Content { Id = blobId, Title = c.Title, Note = c.Note, ReverseName = c.ReverseName, DeviceLabel = c.DeviceLabel, UserName = c.UserName }, Spike2Json.Relaxed.Content));
                }
            }
            blobs += blobIds.Count;
            foreach (var (slot, w) in new[] { ("full", full), ("struct", structure), ("content", content), ("b1", b1), ("b2", b2), ("e3", e3), ("c3", c3) }) Js.Stage(slot, w);
            Phase("setupSerializeMs", Js.Now() - t);
            await Js.SetupBatch(block, block * BlockSize + 1, string.Join('\n', ids), string.Join('\n', blobIds), Csv(fullLens), Csv(structLens), Csv(contentLens));
            Progress = (block + 1) * BlockSize;
            Status = $"stored {Progress} of {events.Count} events in three layouts";
            await _render();
        }
        foreach (var layout in new[] { "L1", "L2", "L3" }) await Js.PutMeta(layout, "setup", "\"" + SetupVersion + "\"");
        Result["events"] = events.Count;
        Result["blobs"] = blobs;
        Result["sizes"] = JsonNode.Parse(Js.SetupSizes());
        Status = "setup done";
        Progress = 0;
    }

    private static int Write(ArrayBufferWriter<byte> w, byte[] bytes) { w.Write(bytes); return bytes.Length; }
    private static string Csv(IEnumerable<int> values) => string.Join(',', values.Select(v => v.ToString(CultureInfo.InvariantCulture)));

    // ---- Cases 1 to 3: replay from blocks ----

    private async Task ReplayCase(string layout, string stores)
    {
        Projection = new ContentProjection();
        var start = Js.Now();
        await ReplayBlocks(layout, stores, Projection);
        await TreeVisible(start);
        Result["checksumOk"] = Projection.Checksum() == ExpectedChecksum;
    }

    /// <summary>Reads blocks in chunks of 10,000 events, parses each line and applies it; with a content store, joins each event with its blob.</summary>
    private async Task ReplayBlocks(string layout, string stores, ContentProjection projection)
    {
        bool joined = stores.Contains(',');
        int seq = 0;
        var lines = new List<BlockLine>(ChunkBlocks * BlockSize);
        var contents = new Dictionary<string, Content>(ChunkBlocks * BlockSize, StringComparer.Ordinal);
        for (int from = 0; ; from += ChunkBlocks)
        {
            int n = await Js.ReadBlocks(layout, stores, from, ChunkBlocks);
            if (n == 0) break;
            var t = Js.Now();
            var eventBuf = Js.TakeBuffer(0);
            var contentBuf = joined ? Js.TakeBuffer(1) : null;
            Phase("copyMs", Js.Now() - t);
            t = Js.Now();
            lines.Clear();
            ForEachLine(eventBuf, span => lines.Add(JsonSerializer.Deserialize(span, Spike2Json.Default.BlockLine)!));
            contents.Clear();
            if (contentBuf != null) ForEachLine(contentBuf, span => { var c = JsonSerializer.Deserialize(span, Spike2Json.Default.Content)!; contents[c.Id!] = c; });
            Phase("parseMs", Js.Now() - t);
            t = Js.Now();
            foreach (var line in lines)
            {
                var content = line.Content;
                if (joined && TextTypes.Contains(line.Event.Type)) contents.TryGetValue(ContentFormat.BlobIdOf(line.Id), out content);
                projection.Apply(line.Id, ++seq, line.Event, content);
            }
            Phase(joined ? "joinApplyMs" : "applyMs", Js.Now() - t);
            Progress = seq;
            Status = $"replayed {seq} events";
            await _render();
        }
    }

    private static void ForEachLine(byte[] buf, SpanAction action)
    {
        int start = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i] != (byte)'\n') continue;
            if (i > start) action(buf.AsSpan(start, i - start));
            start = i + 1;
        }
    }
    private delegate void SpanAction(ReadOnlySpan<byte> span);

    private async Task<double> TreeVisible(double start)
    {
        Status = "tree visible";
        await _render();
        Js.Mark("tree-visible");
        var now = Js.Now();
        Result["replayMs"] = R(now - start);
        Result["treeVisibleMs"] = R(now);
        Result["items"] = Projection!.Items.Count;
        Result["eventsApplied"] = Projection.EventsApplied;
        Result["memoryAfterTree"] = JsonNode.Parse(Js.Memory());
        return now;
    }

    // ---- Case 4: structure first, text of visible items on demand ----

    private async Task StructureFirst()
    {
        Projection = new ContentProjection();
        var start = Js.Now();
        await ReplayBlocks("L3", "blocks", Projection);
        await TreeVisible(start);
        Result["structureMs"] = Result["replayMs"]!.DeepClone();

        var visible = Projection.VisibleTopLevel().Take(VisibleRows).ToList();
        var ids = visible.SelectMany(i => new[] { i.TitleBlob, i.NoteBlob }).Where(b => b != null).Distinct().ToList();
        var t = Js.Now();
        int found = await Js.GetBlobs("L3", string.Join('\n', ids));
        var buf = Js.TakeBuffer(0);
        int k = 0;
        ForEachLineKeepEmpty(buf, span => { if (span.Length > 0) Projection.ApplyContent(ids[k]!, JsonSerializer.Deserialize(span, Spike2Json.Default.Content)!); k++; });
        Status = "text of visible items shown";
        await _render();
        Js.Mark("text-visible");
        var textShown = Js.Now();
        Phase("visibleTextMs", textShown - t);
        Result["textMs"] = R(textShown - start);
        Result["visibleItems"] = visible.Count;
        Result["visibleBlobs"] = ids.Count;
        Result["visibleBlobsFound"] = found;
        Result["visibleTitlesShown"] = visible.Count(i => i.Title != null);

        // The rest of the text in the background (search, notes): content blocks joined into the projection.
        t = Js.Now();
        int seq = 0;
        for (int from = 0; ; from += ChunkBlocks)
        {
            int n = await Js.ReadBlocks("L3", "cblocks", from, ChunkBlocks);
            if (n == 0) break;
            var cbuf = Js.TakeBuffer(0);
            ForEachLine(cbuf, span => { var c = JsonSerializer.Deserialize(span, Spike2Json.Default.Content)!; Projection.ApplyContent(c.Id!, c); seq++; });
            Status = $"text joined: {seq} blobs";
            await _render();
        }
        Result["allTextMs"] = R(Js.Now() - start);
        Phase("backgroundTextMs", Js.Now() - t);
        Result["checksumOk"] = Projection.Checksum() == ExpectedChecksum;
    }

    private static void ForEachLineKeepEmpty(byte[] buf, SpanAction action)
    {
        int start = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i] != (byte)'\n') continue;
            action(buf.AsSpan(start, i - start));
            start = i + 1;
        }
    }

    // ---- Case 5: erase one item ----

    private async Task Erase()
    {
        Projection = new ContentProjection { TrackHistory = true };
        var t = Js.Now();
        await ReplayBlocks("L3", "blocks", Projection);
        Phase("structureReplayForEraseMs", Js.Now() - t);
        await TreeVisible(t);

        var structural = new HashSet<string?>(new[] { Projection.InboxId, Projection.TagsBranchId, Projection.RelationTypesBranchId }, StringComparer.Ordinal);
        var leaves = Projection.Items.Values.Where(i => (i.Children == null || i.Children.Count == 0) && i.History != null && !structural.Contains(i.ParentId) && !structural.Contains(i.Id))
            .OrderBy(i => i.History!.Count).ThenBy(i => i.Id, StringComparer.Ordinal).ToList();
        var leaf = leaves[leaves.Count / 2];
        var branch = Projection.TopLevel.Where(id => !structural.Contains(id)).Select(id => (Id: id, Size: Projection.Branch(id).Count))
            .OrderBy(b => Math.Abs(b.Size - 100)).ThenBy(b => b.Id, StringComparer.Ordinal).First();

        var results = new JsonObject();
        foreach (var (name, itemId) in new[] { ("leaf", leaf.Id), ("branch", branch.Id) })
            foreach (var layout in new[] { "L2", "L3" })
            {
                Status = $"erasing the {name} in {layout}";
                await _render();
                results[$"{layout}-{name}"] = await EraseOne(layout, itemId);
            }
        results["leafItems"] = 1;
        results["branchItems"] = branch.Size;
        Result["erase"] = results;
        Status = "erase measured";
    }

    private async Task<JsonObject> EraseOne(string layout, string itemId)
    {
        var blobs = Projection!.ErasableBlobs(itemId);
        var erased = new HashSet<string>(blobs.Select(b => b.BlobId), StringComparer.Ordinal);
        var blocks = blobs.Select(b => (b.Seq - 1) / BlockSize).Distinct().Order().ToList();
        var store = layout == "L2" ? "blocks" : "cblocks";
        var blobIds = string.Join('\n', erased);
        var indices = Csv(blocks);
        await Js.EraseSave(layout, store, blobIds, indices);
        var before = JsonNode.Parse(Js.Stats())!.AsObject();

        var t0 = Js.Now();
        var lengths = (await Js.ReadBlocksAt(layout, store, indices)).Split(',').Select(int.Parse).ToList();
        var buf = Js.TakeBuffer(0);
        var t = Js.Now();
        var rebuilt = new ArrayBufferWriter<byte>();
        var newLengths = new List<int>();
        int offset = 0;
        foreach (var len in lengths)
        {
            int startCount = rebuilt.WrittenCount;
            var blockBytes = buf.AsSpan(offset, len).ToArray();
            ForEachLine(blockBytes, span => Rewrite(layout, span, erased, rebuilt));
            newLengths.Add(rebuilt.WrittenCount - startCount);
            offset += len;
        }
        Js.Stage("blocks", rebuilt);
        var rewriteMs = Js.Now() - t;
        await Js.EraseCommit(layout, store, blobIds, indices, Csv(newLengths), "080b131c-0001-4fff-8000-0000000e2a5e");
        var eraseMs = Js.Now() - t0;
        var after = JsonNode.Parse(Js.Stats())!.AsObject();

        // Check (not timed): no erased text is left in the rebuilt blocks, and no erased blob can be read.
        var check = (await Js.ReadBlocksAt(layout, store, indices)).Split(',').Select(int.Parse).Sum();
        var checkBuf = Js.TakeBuffer(0);
        int left = 0;
        ForEachLine(checkBuf, span =>
        {
            if (layout == "L2") { var l = JsonSerializer.Deserialize(span, Spike2Json.Default.BlockLine)!; if (l.Content != null && erased.Contains(ContentFormat.BlobIdOf(l.Id))) left++; }
            else { var c = JsonSerializer.Deserialize(span, Spike2Json.Default.Content)!; if (erased.Contains(c.Id!)) left++; }
        });
        int readable = await Js.GetBlobs(layout, blobIds);
        Js.TakeBuffer(0);
        await Js.EraseRestore();

        double Delta(string key) => R((after[key]?.GetValue<double>() ?? 0) - (before[key]?.GetValue<double>() ?? 0));
        return new JsonObject
        {
            ["blobs"] = erased.Count, ["blocks"] = blocks.Count, ["eraseMs"] = R(eraseMs),
            ["readMs"] = Delta("eraseReadMs"), ["decryptMs"] = Delta("eraseDecryptMs"), ["rewriteMs"] = R(rewriteMs),
            ["encryptMs"] = Delta("eraseEncryptMs"), ["writeMs"] = Delta("eraseWriteMs"),
            ["verified"] = left == 0 && readable == 0 && check > 0,
        };
    }

    /// <summary>
    /// Drops the erased text from one block line. Every line starts with its id (<c>{"id":"…"</c>), so only the lines
    /// of erased blobs are parsed; the others are copied as they are.
    /// </summary>
    private static void Rewrite(string layout, ReadOnlySpan<byte> line, HashSet<string> erased, ArrayBufferWriter<byte> into)
    {
        var id = LineId(line);
        bool hit = erased.Contains(layout == "L2" ? ContentFormat.BlobIdOf(id) : id);
        if (hit && layout == "L2")
        {
            var l = JsonSerializer.Deserialize(line, Spike2Json.Default.BlockLine)!;
            l.Content = null;
            into.Write(ContentFormat.Line(l, Spike2Json.Relaxed.BlockLine));
            return;
        }
        if (hit) return;
        into.Write(line);
        into.Write("\n"u8);
    }

    private static string LineId(ReadOnlySpan<byte> line)
    {
        var prefix = "{\"id\":\""u8;
        if (!line.StartsWith(prefix)) throw new FormatException("block line without leading id");
        var rest = line[prefix.Length..];
        return Encoding.UTF8.GetString(rest[..rest.IndexOf((byte)'"')]);
    }

    // ---- Case 6: writing a sync batch of 1,000 events ----

    private async Task Sync()
    {
        // Not timed: the batch is the last 1,000 events of the dataset, read back from layout 2's last block.
        await Js.ReadBlocks("L2", "blocks", Events / BlockSize - 1, 1);
        var batch = new List<BlockLine>();
        ForEachLine(Js.TakeBuffer(0), span => batch.Add(JsonSerializer.Deserialize(span, Spike2Json.Default.BlockLine)!));
        var full = batch.Select(l =>
        {
            var e = JsonSerializer.Deserialize(JsonSerializer.SerializeToUtf8Bytes(l.Event, Spike2Json.Default.EventRecord), Spike2Json.Default.EventRecord)!;
            if (l.Content != null) ContentFormat.Merge(e, l.Content);
            return e;
        }).ToList();

        int rounds = int.TryParse(Js.Query("rounds"), out var rr) ? rr : 5;
        var variants = new[] { "A-L1", "B-L2", "B-L3" };
        var samples = variants.ToDictionary(v => v, _ => new List<JsonObject>());
        for (int round = 0; round < rounds; round++)
        {
            for (int k = 0; k < variants.Length; k++)
            {
                var variant = variants[(k + round) % variants.Length];
                var prefix = (0xf0000000u + (uint)round).ToString("x8", CultureInfo.InvariantCulture);
                var ids = batch.Select(l => prefix + l.Id[8..]).ToList();
                Status = $"sync batch {variant}, round {round + 1} of {rounds}";
                await _render();
                var t0 = Js.Now();
                var events = new ArrayBufferWriter<byte>(); var blobs = new ArrayBufferWriter<byte>(); var b = new ArrayBufferWriter<byte>(); var c = new ArrayBufferWriter<byte>();
                var eventLens = new List<int>(); var blobLens = new List<int>(); var blobIds = new List<string>();
                for (int i = 0; i < batch.Count; i++)
                {
                    if (variant == "A-L1")
                    {
                        eventLens.Add(Write(events, JsonSerializer.SerializeToUtf8Bytes(new PayloadWrapper { Event = full[i] }, Spike2Json.Relaxed.PayloadWrapper)));
                        Write(b, ContentFormat.Line(new BlockLine { Id = ids[i], Event = full[i] }, Spike2Json.Relaxed.BlockLine));
                        continue;
                    }
                    var line = batch[i];
                    eventLens.Add(Write(events, JsonSerializer.SerializeToUtf8Bytes(new PayloadWrapper { Event = line.Event }, Spike2Json.Relaxed.PayloadWrapper)));
                    if (line.Content != null)
                    {
                        var blobId = ContentFormat.BlobIdOf(ids[i]);
                        blobIds.Add(blobId);
                        blobLens.Add(Write(blobs, JsonSerializer.SerializeToUtf8Bytes(line.Content, Spike2Json.Relaxed.Content)));
                    }
                    if (variant == "B-L2") Write(b, ContentFormat.Line(new BlockLine { Id = ids[i], Event = line.Event, Content = line.Content }, Spike2Json.Relaxed.BlockLine));
                    else
                    {
                        Write(b, ContentFormat.Line(new BlockLine { Id = ids[i], Event = line.Event }, Spike2Json.Relaxed.BlockLine));
                        if (line.Content != null) Write(c, ContentFormat.Line(new Content { Id = ContentFormat.BlobIdOf(ids[i]), Title = line.Content.Title, Note = line.Content.Note, ReverseName = line.Content.ReverseName, DeviceLabel = line.Content.DeviceLabel, UserName = line.Content.UserName }, Spike2Json.Relaxed.Content));
                    }
                }
                Js.Stage("events", events);
                Js.Stage("blobs", blobs);
                Js.Stage("b", b);
                if (variant == "B-L3") Js.Stage("c", c);
                var serializeMs = Js.Now() - t0;
                var r = JsonNode.Parse(await Js.SyncWrite(variant[2..], Events / BlockSize, Events + 1, string.Join('\n', ids), string.Join('\n', blobIds), Csv(eventLens), Csv(blobLens)))!.AsObject();
                r["totalMs"] = R(Js.Now() - t0);
                r["serializeMs"] = R(serializeMs);
                r["savedMs"] = R(serializeMs + r["encryptMs"]!.GetValue<double>() + r["storeMs"]!.GetValue<double>());
                samples[variant].Add(r);
                await Js.SyncCleanup();
            }
        }
        var outObj = new JsonObject();
        foreach (var v in variants)
        {
            var list = samples[v];
            var o = new JsonObject { ["events"] = list[0]["events"]!.DeepClone(), ["blobs"] = list[0]["blobs"]!.DeepClone(), ["wireBytes"] = list[0]["wireBytes"]!.DeepClone(), ["paddedBytes"] = list[0]["paddedBytes"]!.DeepClone() };
            foreach (var key in new[] { "serializeMs", "encryptMs", "storeMs", "blockMs", "savedMs", "totalMs" })
            {
                var values = list.Select(x => x[key]!.GetValue<double>()).Order().ToList();
                o[key] = R(values.Count % 2 == 1 ? values[values.Count / 2] : (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2);
            }
            outObj[v] = o;
        }
        Result["sync"] = outObj;
        Result["syncRounds"] = rounds;
        Status = "sync measured";
    }

    public async Task<string> FinishAsync(double appReady, string? error)
    {
        Result["appReadyMs"] = R(appReady);
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
