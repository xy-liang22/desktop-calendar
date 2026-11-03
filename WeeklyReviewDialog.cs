using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static class WeeklyReviewDialog
{
    internal sealed class Section : StackPanel
    {
        internal readonly Guid Id;
        internal readonly TextBox Heading = new() { MinHeight = 28 };
        internal readonly TextBox Body = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 90, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        internal Section(ReviewSection value, Action remove) { Id = value.Id; Heading.Text = value.Heading; Body.Text = value.Markdown; Children.Add(Heading); Children.Add(Body); Children.Add(PlannerView.Button(MainWindow.Text("ReviewRemoveSection"), remove)); }
        internal ReviewSection Value => new(Id, Heading.Text.Trim(), Body.Text);
    }
    internal static bool Edit(MainWindow owner, PlannerController c, WeekKey week)
    {
        if (!c.Writable) return false; var original = c.State!.Reviews.FirstOrDefault(r => r.Week == week); var value = original ?? new WeeklyReview { Id = Guid.NewGuid(), Week = week };
        var s = new PlannerDialogs.Sheet(MainWindow.Text("ReviewEdit"), 740); s.Body.Children.Add(PlannerView.Text(week.Start.ToString("yyyy-MM-dd") + " · " + week.TimeZoneId, 11));
        var notes = new NoteEditor(c, new() { Format = NoteFormat.Markdown, Text = value.Markdown, AssetIds = value.AssetIds }, s); notes.Format.SelectedIndex = (int)NoteFormat.Markdown; notes.Format.Visibility = Visibility.Collapsed; s.Field("ReviewBody", notes);
        var sections = new List<Section>(); var holder = new StackPanel(); s.Field("ReviewSections", holder);
        void Add(ReviewSection row) { Section fields = null!; fields = new(row, () => { sections.Remove(fields); holder.Children.Remove(fields); }); sections.Add(fields); holder.Children.Add(fields); }
        foreach (var row in value.Sections) Add(row); s.Body.Children.Add(PlannerView.Button(MainWindow.Text("ReviewAddSection"), () => Add(new(Guid.NewGuid(), "", ""))));
        var pending = new List<string>(); var snapshots = new StackPanel(); s.Field("ReviewSnapshots", snapshots);
        void RenderStats()
        {
            snapshots.Children.Clear(); foreach (string text in value.Statistics.Select(x => x.Markdown).Concat(pending)) snapshots.Children.Add(new FlowDocumentScrollViewer { Document = MarkdownNotes.Render(text, NoteFormat.Markdown, notes.Images), IsToolBarVisible = false, MaxHeight = 220 });
        }
        s.Body.Children.Add(PlannerView.Button(MainWindow.Text("ReviewInsertStats"), () => { pending.Add(WeekSummary.Markdown(c.State!, week)); RenderStats(); })); RenderStats();
        string Signature() => JsonSerializer.Serialize(new { Text = notes.Source.Text, Sections = sections.Select(x => x.Value).ToArray(), Statistics = pending.ToArray() }); string initial = Signature();
        s.Save(async () =>
        {
            if (Signature() == initial && (original != null || s.CloseWithoutChanges && notes.Source.Text.Length == 0 && sections.Count == 0 && pending.Count == 0)) return true;
            if (sections.Any(x => !TodoDraftRules.Title(x.Heading.Text))) { s.Error.Text = MainWindow.Text("ReviewInvalid"); return false; }
            if (JsonSerializer.Serialize(c.State!.Reviews.FirstOrDefault(r => r.Week == week)) != JsonSerializer.Serialize(original)) { s.Error.Text = MainWindow.Text("PlannerStaleEdit"); return false; }
            var assetIds = notes.Images.Referenced(notes.Source.Text + "\n" + string.Join('\n', sections.Select(x => x.Heading.Text + "\n" + x.Body.Text)) + "\n" + string.Join('\n', value.Statistics.Select(x => x.Markdown).Concat(pending))).ToHashSet(); var commands = new List<PlannerCommand>();
            foreach (var asset in notes.Images.Pending.Where(a => assetIds.Contains(a.Id) && !c.State.Assets.Any(x => x.Id == a.Id))) { if (notes.Images.Read(asset.RelativePath) == null) { s.Error.Text = MainWindow.Text("ActivityImageError"); return false; } commands.Add(new SaveAsset(asset)); }
            commands.Add(new SaveReview(value with { Markdown = notes.Source.Text, Sections = [.. sections.Select(x => x.Value)], AssetIds = [.. assetIds] }));
            commands.AddRange(c.State.Goals.Where(g => g.Week == week && g.ReviewId == null).Select(g => new SaveGoal(g with { ReviewId = value.Id })));
            commands.AddRange(pending.Select(text => new AppendStatistics(value.Id, text)));
            bool ok = await c.ExecuteAsync(new BatchCommand([.. commands])); if (!ok) s.Error.Text = MainWindow.Text(c.NoticeKey); return ok;
        }, true);
        return PlannerDialogs.Show(owner, s) == true;
    }
}
