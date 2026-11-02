using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace DesktopCalendar.Core;

// One deterministic transaction pass, never mutually observing UI setters.
public static class TodoCompletion
{
    static PlannerState Put(PlannerState s, Todo t) => s with { Todos = [.. s.Todos.Select(x => x.Id == t.Id ? t : x)] };
    internal static ImmutableArray<Todo> Descendants(PlannerState s, Todo root)
    {
        var rows = new List<Todo>(); var seen = new HashSet<Guid> { root.Id };
        void Walk(Todo parent)
        {
            foreach (var child in PlannerQueries.Children(s, parent))
            { if (!seen.Add(child.Id)) throw new PlannerException("ParentCycle"); rows.Add(child); Walk(child); }
        }
        Walk(root); return [.. rows];
    }
    public static string Guard(PlannerState s, Todo root) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
        Descendants(s, root).OrderBy(t => t.Id).Select(t => new { t.Id, t.ParentId, t.Status, t.CompletionIntent, t.CompletedAt, t.FrozenCarryDate, t.ProgressMode, t.ManualProgress }))));
    internal static PlannerState Set(PlannerState s, Todo root, bool completed, DateTimeOffset now)
    {
        if (root.Status == TodoStatus.Cancelled) throw new PlannerException("CancelledTodo");
        var descendants = Descendants(s, root);
        if (completed)
        {
            if (root.Status == TodoStatus.Completed && root.CompletionIntent == CompletionIntent.ManualComplete) return s;
            var before = descendants.Select(t => new CompletionBefore(t.Id, t.Status, t.CompletionIntent, t.CompletedAt, t.FrozenCarryDate)).ToImmutableArray();
            foreach (var child in descendants.Where(t => t.Status != TodoStatus.Cancelled))
                s = Put(s, Finish(child with { CompletionIntent = CompletionIntent.CascadeComplete }, true, now, s.Options.TimeZoneId));
            root = Finish(root with { CompletionIntent = CompletionIntent.ManualComplete, CompletionSnapshot = null, CompletionSnapshotInvalidated = false }, true, now, s.Options.TimeZoneId);
            s = Put(s, root);
            if (descendants.Length > 0) s = Put(s, root with { CompletionSnapshot = new(Guid.NewGuid(), now, Guard(s, root), before) });
        }
        else
        {
            var snapshot = root.CompletionSnapshot;
            bool valid = snapshot != null && snapshot.Guard == Guard(s, root);
            if (valid)
                foreach (var entry in snapshot!.Entries)
                {
                    var child = s.Todos.Single(t => t.Id == entry.TodoId);
                    s = Put(s, child with { Status = entry.Status, CompletionIntent = entry.Intent, CompletedAt = entry.CompletedAt, FrozenCarryDate = entry.FrozenCarryDate, ModifiedAt = now });
                }
            root = Finish(root with { CompletionIntent = CompletionIntent.ManualIncomplete, CompletionSnapshot = null,
                CompletionSnapshotInvalidated = root.CompletionSnapshotInvalidated || snapshot != null && !valid }, false, now, s.Options.TimeZoneId);
            s = Put(s, root);
        }
        return s;
    }
    static Todo Finish(Todo t, bool completed, DateTimeOffset now, string zone)
    {
        DateOnly? freeze = completed && t.Kind is TodoKind.CarryHistory or TodoKind.CarryLatest
            ? t.FrozenCarryDate ?? new[] { t.PlannedDate, t.CarryDate ?? t.PlannedDate, PlannerTime.LocalDate(now, zone) }.Max() : null;
        return t with { Status = completed ? TodoStatus.Completed : TodoStatus.Pending, CompletedAt = completed ? t.CompletedAt ?? now : null, FrozenCarryDate = freeze, ModifiedAt = now };
    }
    internal static PlannerState Reconcile(PlannerState before, PlannerState s, DateTimeOffset now, HashSet<Guid>? protectedIntents = null)
    {
        var old = before.Todos.ToDictionary(t => t.Id); var current = s.Todos.ToDictionary(t => t.Id);
        var independent = old.Keys.Union(current.Keys).Where(id => !old.TryGetValue(id, out var a) || !current.TryGetValue(id, out var b) ||
            a.ParentId != b.ParentId || a.Status != b.Status || a.CompletionIntent != b.CompletionIntent || a.ManualProgress != b.ManualProgress || a.ProgressMode != b.ProgressMode).ToArray();
        var clear = new HashSet<Guid>();
        void Ancestors(Dictionary<Guid, Todo> map, Guid id)
        { var seen = new HashSet<Guid>(); while (map.TryGetValue(id, out var t) && t.ParentId is Guid p) { if (!seen.Add(p)) throw new PlannerException("ParentCycle"); clear.Add(p); id = p; } }
        foreach (var id in independent) { Ancestors(old, id); Ancestors(current, id); }
        foreach (var parent in s.Todos.Where(t => old.ContainsKey(t.Id)))
        {
            var prior = PlannerQueries.Children(before, old[parent.Id]).Select(t => t.Id).Order();
            if (!prior.SequenceEqual(PlannerQueries.Children(s, parent).Select(t => t.Id).Order())) clear.Add(parent.Id);
        }
        foreach (var id in clear.Where(id => protectedIntents?.Contains(id) != true))
            if (s.Todos.SingleOrDefault(t => t.Id == id) is Todo parent && (parent.CompletionIntent is CompletionIntent.ManualIncomplete or CompletionIntent.CascadeComplete || parent.CompletionIntent == CompletionIntent.ManualComplete && parent.CompletionSnapshot == null))
                s = Put(s, parent with { CompletionIntent = CompletionIntent.Automatic, ModifiedAt = now });
        for (int pass = 0; pass < 260; pass++)
        {
            bool changed = false;
            foreach (var node in s.Todos.Where(t => t.CompletionIntent != CompletionIntent.CascadeComplete && t.CompletionSnapshot != null).ToArray())
            {
                if (node.CompletionSnapshot!.Guard == Guard(s, node)) continue;
                var ids = node.CompletionSnapshot.Entries.Select(e => e.TodoId).ToHashSet();
                foreach (var child in s.Todos.Where(t => ids.Contains(t.Id) && t.CompletionIntent == CompletionIntent.CascadeComplete).ToArray())
                {
                    bool nestedValid = child.CompletionSnapshot != null && child.CompletionSnapshot.Guard == Guard(s, child);
                    var intent = nestedValid || !PlannerQueries.Children(s, child).Any(t => t.Status != TodoStatus.Cancelled) ? CompletionIntent.ManualComplete : CompletionIntent.Automatic;
                    s = Put(s, child with { CompletionIntent = intent, ModifiedAt = now });
                }
                var actual = s.Todos.Single(t => t.Id == node.Id);
                s = Put(s, actual with { CompletionSnapshot = null, CompletionSnapshotInvalidated = true,
                    CompletionIntent = actual.CompletionIntent == CompletionIntent.ManualComplete ? CompletionIntent.Automatic : actual.CompletionIntent, ModifiedAt = now }); changed = true;
            }
            // Deleting/promoting a cascade owner must not leave forced provenance orphaned.
            var covered = s.Todos.Where(t => t.CompletionSnapshot != null).SelectMany(t => t.CompletionSnapshot!.Entries).Select(e => e.TodoId).ToHashSet();
            foreach (var child in s.Todos.Where(t => t.CompletionIntent == CompletionIntent.CascadeComplete && !covered.Contains(t.Id)).ToArray())
            {
                bool nestedValid = child.CompletionSnapshot != null && child.CompletionSnapshot.Guard == Guard(s, child);
                var intent = nestedValid || !PlannerQueries.Children(s, child).Any(t => t.Status != TodoStatus.Cancelled) ? CompletionIntent.ManualComplete : CompletionIntent.Automatic;
                s = Put(s, child with { CompletionIntent = intent, ModifiedAt = now }); changed = true;
            }
            var visiting = new HashSet<Guid>(); var done = new HashSet<Guid>();
            void Normalize(Guid id)
            {
                if (done.Contains(id)) return;
                if (!visiting.Add(id)) throw new PlannerException("ParentCycle");
                var node = s.Todos.Single(t => t.Id == id);
                var children = PlannerQueries.Children(s, node).Where(t => t.Status != TodoStatus.Cancelled).ToArray();
                foreach (var child in children) if (s.Todos.Any(t => t.Id == child.Id)) Normalize(child.Id);
                node = s.Todos.Single(t => t.Id == id);
                if (node.Status != TodoStatus.Cancelled)
                {
                    bool completed = node.CompletionIntent is CompletionIntent.ManualComplete or CompletionIntent.CascadeComplete || node.CompletionIntent == CompletionIntent.Automatic && children.Length > 0 && PlannerQueries.Children(s, node).Where(t => t.Status != TodoStatus.Cancelled).All(t => t.Status == TodoStatus.Completed);
                    if ((node.Status == TodoStatus.Completed) != completed) { s = Put(s, Finish(node, completed, now, s.Options.TimeZoneId)); changed = true; }
                }
                visiting.Remove(id); done.Add(id);
            }
            foreach (var id in s.Todos.Select(t => t.Id).ToArray()) Normalize(id);
            if (!changed) return s;
        }
        throw new PlannerException("CompletionDidNotConverge");
    }
}
