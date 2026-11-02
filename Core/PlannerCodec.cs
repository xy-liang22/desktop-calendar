using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace DesktopCalendar.Core;

// Storage DTOs deliberately contain wire elements, never controls or mutable live entities.
// All wire/domain mapping and schema changes are centralized here. Runtime options live
// in the separate application preferences and are supplied on open, not copied into data.
internal sealed record PlannerPayloadDto(JsonElement Categories, JsonElement Todos, JsonElement Series,
    JsonElement Activities, JsonElement Goals, JsonElement Reviews, JsonElement Assets, JsonElement Decorations, JsonElement TodoOrderings);
internal sealed record PlannerEnvelopeDto(int SchemaVersion, Guid DocumentId, long Revision, PlannerPayloadDto Payload, string PayloadSha256);
internal sealed record LegacyPlannerDto(int SchemaVersion, Guid DocumentId, PlannerPayloadDto Data);
public sealed record DecodedPlanner(PlannerState State, bool Migrated);

public static class PlannerCodec
{
    public const int CurrentSchema = 3;
    internal static readonly JsonSerializerOptions Json = MakeOptions();
    static JsonSerializerOptions MakeOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Kind == JsonTypeInfoKind.Object)
                foreach (var property in info.Properties.Where(p => p.Set != null)) property.IsRequired = true;
        });
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true, IgnoreReadOnlyProperties = true, TypeInfoResolver = resolver, MaxDepth = 64,
            Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
        };
    }
    static PlannerPayloadDto Payload(PlannerState state) => new(
        JsonSerializer.SerializeToElement(state.Categories, Json), JsonSerializer.SerializeToElement(state.Todos, Json),
        JsonSerializer.SerializeToElement(state.Series, Json), JsonSerializer.SerializeToElement(state.Activities, Json),
        JsonSerializer.SerializeToElement(state.Goals, Json), JsonSerializer.SerializeToElement(state.Reviews, Json), JsonSerializer.SerializeToElement(state.Assets, Json), JsonSerializer.SerializeToElement(state.Decorations, Json), JsonSerializer.SerializeToElement(state.TodoOrderings, Json));
    static string Hash(PlannerPayloadDto payload) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload, Json)));
    public static byte[] Encode(PlannerState state)
    {
        PlannerIntegrity.Validate(state); var payload = Payload(state);
        return JsonSerializer.SerializeToUtf8Bytes(new PlannerEnvelopeDto(CurrentSchema, state.Id, state.Revision, payload, Hash(payload)), Json);
    }
    public static byte[] EncodeLegacyV1ForMigration(PlannerState state)
    {
        PlannerIntegrity.Validate(state);
        return JsonSerializer.SerializeToUtf8Bytes(new LegacyPlannerDto(1, state.Id, Payload(state)), Json);
    }
    public static byte[] EncodeLegacyV2ForMigration(PlannerState state)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(Payload(state), Json))!;
        void Strip(System.Text.Json.Nodes.JsonNode? todo)
        { if (todo == null) return; todo.AsObject().Remove("completionIntent"); todo.AsObject().Remove("completionSnapshot"); todo.AsObject().Remove("completionSnapshotInvalidated"); if (todo["progressMode"]?.GetValue<string>() == "Default") todo["progressMode"] = "Manual"; }
        foreach (var t in node["todos"]!.AsArray()) Strip(t);
        foreach (var series in node["series"]!.AsArray()) foreach (var version in series!["versions"]!.AsArray()) foreach (var t in version!["templates"]!.AsArray()) Strip(t);
        var payload = node.Deserialize<PlannerPayloadDto>(Json)!;
        return JsonSerializer.SerializeToUtf8Bytes(new PlannerEnvelopeDto(2, state.Id, state.Revision, payload, Hash(payload)), Json);
    }
    public static DecodedPlanner Decode(ReadOnlySpan<byte> bytes, PlannerOptions? options = null)
    {
        using var document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 64 });
        var root = document.RootElement;
        if (!root.TryGetProperty("schemaVersion", out var versionElement) || !versionElement.TryGetInt32(out int version)) throw new PlannerException("MissingSchema");
        if (version > CurrentSchema) throw new PlannerException("NewerSchema");
        RejectDuplicateProperties(root);
        PlannerPayloadDto payload; Guid id; long revision;
        if (version == 1)
        {
            if (root.EnumerateObject().Select(p => p.Name).Except(["schemaVersion", "documentId", "data"]).Any()) throw new PlannerException("UnknownLegacyFields");
            id = root.GetProperty("documentId").GetGuid(); revision = 0;
            // v1 predates date decorations and retired template identity tombstones.
            var legacy = System.Text.Json.Nodes.JsonNode.Parse(root.GetProperty("data").GetRawText())!;
            legacy["decorations"] ??= new System.Text.Json.Nodes.JsonArray();
            legacy["todoOrderings"] ??= new System.Text.Json.Nodes.JsonArray();
            if (legacy["series"] is System.Text.Json.Nodes.JsonArray series)
                foreach (var row in series) if (row != null) row["retiredNodeIds"] ??= new System.Text.Json.Nodes.JsonArray();
            payload = legacy.Deserialize<PlannerPayloadDto>(Json) ?? throw new PlannerException("InvalidPayload");
        }
        else if (version is 2 or CurrentSchema)
        {
            var dto = root.Deserialize<PlannerEnvelopeDto>(Json) ?? throw new PlannerException("InvalidEnvelope");
            if (!string.Equals(dto.PayloadSha256, Hash(dto.Payload), StringComparison.Ordinal)) throw new PlannerException("ChecksumMismatch");
            payload = dto.Payload; id = dto.DocumentId; revision = dto.Revision;
        }
        else throw new PlannerException("UnsupportedSchema");
        if (version < CurrentSchema)
        {
            var migrated = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(payload, Json))!;
            void Upgrade(System.Text.Json.Nodes.JsonNode? todo)
            {
                if (todo == null) return;
                todo["completionIntent"] ??= todo["status"]!.GetValue<string>() == "Completed" ? "ManualComplete" : "Automatic";
                if (!todo.AsObject().ContainsKey("completionSnapshot")) todo["completionSnapshot"] = null;
                todo["completionSnapshotInvalidated"] ??= false;
                if (todo["progressMode"]!.GetValue<string>() == "Manual" && todo["manualProgress"]!.GetValue<decimal>() == 0) todo["progressMode"] = "Default";
            }
            foreach (var t in migrated["todos"]!.AsArray()) Upgrade(t);
            foreach (var series in migrated["series"]!.AsArray()) foreach (var v in series!["versions"]!.AsArray()) foreach (var t in v!["templates"]!.AsArray()) Upgrade(t);
            payload = migrated.Deserialize<PlannerPayloadDto>(Json)!;
        }
        ImmutableArray<T> Array<T>(JsonElement rows) => rows.Deserialize<ImmutableArray<T>>(Json);
        var state = new PlannerState { Id = id, Revision = revision, Options = options ?? new() { TimeZoneId = TimeZoneInfo.Local.Id },
            Categories = Array<Category>(payload.Categories), Todos = Array<Todo>(payload.Todos), Series = Array<TodoSeries>(payload.Series),
            Activities = Array<Activity>(payload.Activities), Goals = Array<WeekGoal>(payload.Goals), Reviews = Array<WeeklyReview>(payload.Reviews), Assets = Array<LocalAsset>(payload.Assets), Decorations = Array<DayDecoration>(payload.Decorations), TodoOrderings = Array<TodoOrdering>(payload.TodoOrderings) };
        if (version < CurrentSchema) state = TodoCompletion.Reconcile(state, state, state.Todos.Select(t => t.ModifiedAt).DefaultIfEmpty(DateTimeOffset.UnixEpoch).Max());
        PlannerIntegrity.Validate(state);
        return new(state, version < CurrentSchema);
    }
    static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            { if (!names.Add(property.Name)) throw new PlannerException("DuplicateJsonProperty"); RejectDuplicateProperties(property.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) RejectDuplicateProperties(child);
    }
}
