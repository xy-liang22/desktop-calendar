using System;
using System.Linq;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;

internal sealed record TimeZoneChoice(string Id, string Name);
internal static class TimeZoneChoices
{
    internal static string Resolve(string id)
    { try { return id.Length == 0 ? TimeZoneInfo.Local.Id : PlannerTime.Zone(id).Id; } catch (PlannerException) { return TimeZoneInfo.Local.Id; } }
    internal static ComboBox Create(string? selected, string emptyKey)
    {
        var choices = new[] { new TimeZoneChoice("", MainWindow.Text(emptyKey)) }.Concat(TimeZoneInfo.GetSystemTimeZones().Select(z => new TimeZoneChoice(z.Id, z.Id + " · " + z.DisplayName))).ToList();
        if (!string.IsNullOrEmpty(selected) && choices.All(c => c.Id != selected)) choices.Add(new(selected, selected));
        return new ComboBox { ItemsSource = choices, DisplayMemberPath = nameof(TimeZoneChoice.Name), SelectedValuePath = nameof(TimeZoneChoice.Id), SelectedValue = selected ?? "", MinHeight = 28, MaxDropDownHeight = 300 };
    }
    internal static string Value(ComboBox box) => (box.SelectedItem as TimeZoneChoice)?.Id ?? "";
}
