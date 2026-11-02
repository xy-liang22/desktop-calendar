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
    JsonElement Activities, JsonElement Goals, JsonElement Reviews, JsonElement Assets);
internal sealed record PlannerEnvelopeDto(int SchemaVersion, Guid DocumentId, long Revision, PlannerPayloadDto Payload, string PayloadSha256);
internal sealed record LegacyPlannerDto(int SchemaVersion, Guid DocumentId, PlannerPayloadDto Data);
public sealed record DecodedPlanner(PlannerState State, bool Migrated);

public static class PlannerCodec
{
    public const int CurrentSchema = 2;
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
        JsonSerializer.SerializeToElement(state.Goals, Json), JsonSerializer.SerializeToElement(state.Reviews, Json), JsonSerializer.SerializeToElement(state.Assets, Json));
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
            payload = root.GetProperty("data").Deserialize<PlannerPayloadDto>(Json) ?? throw new PlannerException("InvalidPayload");
        }
        else if (version == CurrentSchema)
        {
            var dto = root.Deserialize<PlannerEnvelopeDto>(Json) ?? throw new PlannerException("InvalidEnvelope");
            if (!string.Equals(dto.PayloadSha256, Hash(dto.Payload), StringComparison.Ordinal)) throw new PlannerException("ChecksumMismatch");
            payload = dto.Payload; id = dto.DocumentId; revision = dto.Revision;
        }
        else throw new PlannerException("UnsupportedSchema");
        ImmutableArray<T> Array<T>(JsonElement rows) => rows.Deserialize<ImmutableArray<T>>(Json);
        var state = new PlannerState { Id = id, Revision = revision, Options = options ?? new() { TimeZoneId = TimeZoneInfo.Local.Id },
            Categories = Array<Category>(payload.Categories), Todos = Array<Todo>(payload.Todos), Series = Array<TodoSeries>(payload.Series),
            Activities = Array<Activity>(payload.Activities), Goals = Array<WeekGoal>(payload.Goals), Reviews = Array<WeeklyReview>(payload.Reviews), Assets = Array<LocalAsset>(payload.Assets) };
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
