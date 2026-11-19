using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpikeCore;

/// <summary>Hybrid logical clock value (architecture.md "Ordering of siblings").</summary>
public sealed class Clock
{
    [JsonPropertyName("time")] public long Time { get; set; }
    [JsonPropertyName("counter")] public int Counter { get; set; }
}

/// <summary>
/// The event data of every event type of the current format (architecture.md "Event types", with ADR-082 to ADR-084).
/// One flat class keeps the spike simple; unset fields are not written.
/// </summary>
public sealed class EventData
{
    [JsonPropertyName("itemId")] public string? ItemId { get; set; }
    [JsonPropertyName("parentId")] public string? ParentId { get; set; }
    [JsonPropertyName("afterItemId")] public string? AfterItemId { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
    [JsonPropertyName("format")] public string? Format { get; set; }
    [JsonPropertyName("replaces")] public List<string>? Replaces { get; set; }
    [JsonPropertyName("seen")] public Dictionary<string, Clock>? Seen { get; set; }
    [JsonPropertyName("tagId")] public string? TagId { get; set; }
    [JsonPropertyName("relationId")] public string? RelationId { get; set; }
    [JsonPropertyName("fromItemId")] public string? FromItemId { get; set; }
    [JsonPropertyName("typeId")] public string? TypeId { get; set; }
    [JsonPropertyName("toItemId")] public string? ToItemId { get; set; }
    [JsonPropertyName("symmetric")] public bool? Symmetric { get; set; }
    [JsonPropertyName("reverseName")] public string? ReverseName { get; set; }
    [JsonPropertyName("deviceId")] public string? DeviceId { get; set; }
    [JsonPropertyName("deviceLabel")] public string? DeviceLabel { get; set; }
    [JsonPropertyName("userId")] public string? UserId { get; set; }
    [JsonPropertyName("userName")] public string? UserName { get; set; }
}

/// <summary>The event inside the payload wrapper (architecture.md "Example of an event before encryption").</summary>
public sealed class EventRecord
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("schema")] public int Schema { get; set; } = 1;
    [JsonPropertyName("deviceId")] public string DeviceId { get; set; } = "";
    [JsonPropertyName("actionId")] public string ActionId { get; set; } = "";
    [JsonPropertyName("clock")] public Clock Clock { get; set; } = new();
    [JsonPropertyName("timestamp")] public string Timestamp { get; set; } = "";
    [JsonPropertyName("reverts")] public string? Reverts { get; set; }
    [JsonPropertyName("data")] public EventData Data { get; set; } = new();
}

/// <summary>
/// Payload wrapper (ADR-073): the encrypted object holds the event plus room for optional parts
/// (for example a later device signature). Spike shape: <c>{"v":1,"event":{...}}</c>; optional parts would be added as siblings of "event".
/// </summary>
public sealed class PayloadWrapper
{
    [JsonPropertyName("v")] public int V { get; set; } = 1;
    [JsonPropertyName("event")] public EventRecord Event { get; set; } = new();
}

/// <summary>Plaintext envelope fields (ADR-015). <c>ServerSequence</c> is absent until synced.</summary>
public sealed class Envelope
{
    [JsonPropertyName("v")] public int V { get; set; } = 1;
    [JsonPropertyName("eventId")] public string EventId { get; set; } = "";
    [JsonPropertyName("keyId")] public string KeyId { get; set; } = "";
    [JsonPropertyName("nonce")] public string Nonce { get; set; } = "";
    [JsonPropertyName("payload")] public string Payload { get; set; } = "";
}

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PayloadWrapper))]
[JsonSerializable(typeof(Envelope))]
[JsonSerializable(typeof(SnapshotDto))]
[JsonSerializable(typeof(IndexChunkDto))]
public sealed partial class SpikeJson : JsonSerializerContext
{
    /// <summary>
    /// Relaxed escaping: non-ASCII text (German, emoji) and '+' in base64 stay unescaped. The default encoder would
    /// write every non-ASCII character as \uXXXX and inflate the payload before padding.
    /// </summary>
    public static readonly SpikeJson Relaxed = new(new JsonSerializerOptions
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    });
}
