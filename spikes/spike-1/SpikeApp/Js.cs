using System.Runtime.InteropServices.JavaScript;

namespace SpikeApp;

/// <summary>The only place that reaches the browser (spike stand-in for the adapter namespace, ADR-042).</summary>
[System.Runtime.Versioning.SupportedOSPlatform("browser")]
internal static partial class Js
{
    public const string Module = "spike";

    /// <summary>Loads the interop module relative to the page's base address (the builds live under /interpreted/ and /aot/ on a device site).</summary>
    public static Task Load(string baseUri) => JSHost.ImportAsync(Module, baseUri + "spike/interop.js");

    [JSImport("now", Module)] public static partial double Now();
    [JSImport("mark", Module)] public static partial void Mark(string name);
    [JSImport("stats", Module)] public static partial string Stats();
    [JSImport("resetStats", Module)] public static partial void ResetStats();
    [JSImport("query", Module)] public static partial string Query(string name);
    [JSImport("lastSeq", Module)] public static partial int LastSeq();
    [JSImport("initKey", Module)] public static partial Task InitKey(string seedLabel, string keyId, string accountId);
    [JSImport("fetchText", Module)] public static partial Task<string> FetchText(string url);
    [JSImport("openDb", Module)] public static partial Task OpenDb(string name, string durability);
    [JSImport("deleteDb", Module)] public static partial Task DeleteDb(string name);
    [JSImport("importFile", Module)] public static partial Task<int> ImportFile(string url, int batch);
    [JSImport("readDecrypt", Module)] public static partial Task<int> ReadDecrypt(int afterSeq, int chunk, int batch, bool parallel);
    [JSImport("takeBuffer", Module)] public static partial void TakeBuffer([JSMarshalAs<JSType.MemoryView>] Span<byte> into);
    [JSImport("bufferLength", Module)] public static partial int BufferLength();
    [JSImport("stageBytes", Module)] public static partial void StageBytes([JSMarshalAs<JSType.MemoryView>] Span<byte> bytes);
    [JSImport("encryptAppend", Module)] public static partial Task<int> EncryptAppend(string eventId);
    [JSImport("putSnapshot", Module)] public static partial Task<int> PutSnapshot(int seq);
    [JSImport("getSnapshot", Module)] public static partial Task<int> GetSnapshot();
    [JSImport("addIndexChunk", Module)] public static partial void AddIndexChunk([JSMarshalAs<JSType.MemoryView>] Span<byte> bytes);
    [JSImport("flushIndexChunks", Module)] public static partial Task<int> FlushIndexChunks(string variant);
    [JSImport("loadIndexChunks", Module)] public static partial Task<int> LoadIndexChunks(string variant);
    [JSImport("indexChunkLength", Module)] public static partial int IndexChunkLength(int i);
    [JSImport("takeIndexChunk", Module)] public static partial void TakeIndexChunk(int i, [JSMarshalAs<JSType.MemoryView>] Span<byte> into);
    [JSImport("storageEstimate", Module)] public static partial Task<string> StorageEstimate();
    [JSImport("memory", Module)] public static partial string Memory();
    [JSImport("environment", Module)] public static partial string Environment();
    [JSImport("report", Module)] public static partial void Report(string json);
    [JSImport("blockBench", Module)] public static partial Task<string> BlockBench(string sizes);
    [JSImport("generateBegin", Module)] public static partial void GenerateBegin();
    [JSImport("encryptStoreBatch", Module)] public static partial Task EncryptStoreBatch(string eventIds, string lengths, int firstCounter);
    [JSImport("generateEnd", Module)] public static partial string GenerateEnd();
    [JSImport("deviceHandOff", Module)] public static partial Task DeviceHandOff(string json);
    [JSImport("download", Module)] public static partial void Download(string json, string name);

    public static byte[] TakeBuffer()
    {
        var b = new byte[BufferLength()];
        TakeBuffer(b);
        return b;
    }
}
