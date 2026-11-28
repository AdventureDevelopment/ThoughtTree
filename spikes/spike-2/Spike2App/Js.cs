using System.Runtime.InteropServices.JavaScript;

namespace Spike2App;

/// <summary>The only place that reaches the browser (spike stand-in for the adapter namespace, ADR-042).</summary>
[System.Runtime.Versioning.SupportedOSPlatform("browser")]
internal static partial class Js
{
    public const string Module = "spike2";

    public static Task Load(string baseUri) => JSHost.ImportAsync(Module, baseUri + "spike/interop.js");

    [JSImport("now", Module)] public static partial double Now();
    [JSImport("mark", Module)] public static partial void Mark(string name);
    [JSImport("stats", Module)] public static partial string Stats();
    [JSImport("query", Module)] public static partial string Query(string name);
    [JSImport("initKey", Module)] public static partial Task InitKey(string seedLabel, string keyId, string accountId, string treeId);
    [JSImport("open", Module)] public static partial Task Open(string layouts);
    [JSImport("resetAll", Module)] public static partial Task ResetAll();
    [JSImport("getMeta", Module)] public static partial Task<string> GetMeta(string layout, string key);
    [JSImport("putMeta", Module)] public static partial Task PutMeta(string layout, string key, string json);
    [JSImport("stage", Module)] public static partial void Stage(string slot, [JSMarshalAs<JSType.MemoryView>] Span<byte> bytes);
    [JSImport("bufferLength", Module)] public static partial int BufferLength(int index);
    [JSImport("takeBuffer", Module)] public static partial void TakeBuffer(int index, [JSMarshalAs<JSType.MemoryView>] Span<byte> into);
    [JSImport("setupBatch", Module)] public static partial Task SetupBatch(int block, int firstSeq, string eventIds, string blobIds, string fullLens, string structLens, string contentLens);
    [JSImport("setupSizes", Module)] public static partial string SetupSizes();
    [JSImport("readBlocks", Module)] public static partial Task<int> ReadBlocks(string layout, string stores, int from, int count);
    [JSImport("lastSeq", Module)] public static partial int LastSeq();
    [JSImport("getBlobs", Module)] public static partial Task<int> GetBlobs(string layout, string ids);
    [JSImport("readBlocksAt", Module)] public static partial Task<string> ReadBlocksAt(string layout, string store, string indices);
    [JSImport("eraseSave", Module)] public static partial Task EraseSave(string layout, string store, string blobIds, string indices);
    [JSImport("eraseCommit", Module)] public static partial Task EraseCommit(string layout, string store, string blobIds, string indices, string lengths, string eraseEventId);
    [JSImport("eraseRestore", Module)] public static partial Task EraseRestore();
    [JSImport("syncWrite", Module)] public static partial Task<string> SyncWrite(string layout, int block, int firstSeq, string eventIds, string blobIds, string eventLens, string blobLens);
    [JSImport("syncCleanup", Module)] public static partial Task SyncCleanup();
    [JSImport("storageEstimate", Module)] public static partial Task<string> StorageEstimate();
    [JSImport("environment", Module)] public static partial string Environment();
    [JSImport("memory", Module)] public static partial string Memory();
    [JSImport("report", Module)] public static partial void Report(string json);
    [JSImport("deviceHandOff", Module)] public static partial Task DeviceHandOff(string json);
    [JSImport("download", Module)] public static partial void Download(string json, string name);

    public static void Stage(string slot, System.Buffers.ArrayBufferWriter<byte> writer) =>
        Stage(slot, System.Runtime.InteropServices.MemoryMarshal.AsMemory(writer.WrittenMemory).Span);

    public static byte[] TakeBuffer(int index)
    {
        var b = new byte[BufferLength(index)];
        TakeBuffer(index, b);
        return b;
    }
}
