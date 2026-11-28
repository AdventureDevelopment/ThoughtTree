using System.Text.Json;
using System.Text.Json.Serialization;
using SpikeCore;

namespace Spike2App;

/// <summary>
/// The text a person typed, moved out of an event into a content blob (ADR-091 "Content", Proposed). One blob per
/// event that carries text. As a per-blob payload <see cref="Id"/> is left out (the blob id is a plaintext envelope
/// field); in a content block line it names the blob.
/// </summary>
public sealed class Content
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
    [JsonPropertyName("reverseName")] public string? ReverseName { get; set; }
    [JsonPropertyName("deviceLabel")] public string? DeviceLabel { get; set; }
    [JsonPropertyName("userName")] public string? UserName { get; set; }
}

/// <summary>
/// One line of a local block (ADR-086): the event id (needed to derive the blob id and by the real replay anyway),
/// the event and, in layout 2, the event's content joined in.
/// </summary>
public sealed class BlockLine
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("event")] public EventRecord Event { get; set; } = new();
    [JsonPropertyName("content")] public Content? Content { get; set; }
}

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Content))]
[JsonSerializable(typeof(BlockLine))]
[JsonSerializable(typeof(PayloadWrapper))]
public sealed partial class Spike2Json : JsonSerializerContext
{
    /// <summary>Relaxed escaping as in spike 1: non-ASCII text stays unescaped, so payloads are not inflated before padding.</summary>
    public static readonly Spike2Json Relaxed = new(new JsonSerializerOptions
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    });
}

/// <summary>Spike choices for content blobs; not format decisions.</summary>
public static class ContentFormat
{
    /// <summary>ADR-087 buckets: 512 B, 1, 2, 4, 8, 16 KiB, above 16 KiB a multiple of 16 KiB.</summary>
    public static readonly int[] Buckets = [512, 1024, 2048, 4096, 8192, 16384];

    /// <summary>
    /// Blob id derived from the event id (ADR-091): the event id's kind group "0001" becomes "00cb", so the blob id
    /// keeps the event id's length and is unique per event. Spike choice; the real derivation is open.
    /// </summary>
    public static string BlobIdOf(string eventId) => string.Concat(eventId.AsSpan(0, 9), "00cb", eventId.AsSpan(13));

    /// <summary>Splits an event into its structure (text fields removed) and its content (null when it carries no text).</summary>
    public static (EventRecord Structure, Content? Content) Split(EventRecord e)
    {
        var d = e.Data;
        if (d.Title == null && d.Note == null && d.ReverseName == null && d.DeviceLabel == null && d.UserName == null) return (e, null);
        var content = new Content { Title = d.Title, Note = d.Note, ReverseName = d.ReverseName, DeviceLabel = d.DeviceLabel, UserName = d.UserName };
        var data = new EventData
        {
            ItemId = d.ItemId, ParentId = d.ParentId, AfterItemId = d.AfterItemId, Format = d.Format, Replaces = d.Replaces, Seen = d.Seen,
            TagId = d.TagId, RelationId = d.RelationId, FromItemId = d.FromItemId, TypeId = d.TypeId, ToItemId = d.ToItemId,
            Symmetric = d.Symmetric, DeviceId = d.DeviceId, UserId = d.UserId,
        };
        var structure = new EventRecord { Type = e.Type, Schema = e.Schema, DeviceId = e.DeviceId, ActionId = e.ActionId, Clock = e.Clock, Timestamp = e.Timestamp, Reverts = e.Reverts, Data = data };
        return (structure, content);
    }

    /// <summary>Writes the content's text back into the event's data (the join of replay).</summary>
    public static void Merge(EventRecord e, Content c)
    {
        e.Data.Title = c.Title; e.Data.Note = c.Note; e.Data.ReverseName = c.ReverseName; e.Data.DeviceLabel = c.DeviceLabel; e.Data.UserName = c.UserName;
    }

    public static byte[] Line<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, info);
        var line = new byte[bytes.Length + 1];
        bytes.CopyTo(line, 0);
        line[^1] = (byte)'\n';
        return line;
    }
}
