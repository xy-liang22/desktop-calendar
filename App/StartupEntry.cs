using System;
using System.IO;
using System.Text.Json;
using DesktopCalendar.Core;

namespace DesktopCalendar;
// Explicit current-user action only; no registry, service or global setting.
internal sealed class StartupEntry
{
    const string Description = "Desktop Calendar startup (current user)";
    readonly string install, startup;
    internal StartupEntry(string? installDirectory = null, string? startupDirectory = null)
    {
        install = Path.GetFullPath(installDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "DesktopCalendar"));
        startup = Path.GetFullPath(startupDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.Startup));
    }
    string Link => Path.Combine(startup, "Desktop Calendar.lnk");
    static dynamic Shell() => Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
    bool Owned()
    { if (!File.Exists(Link)) return false; PlannerBackup.SafePath(Link); dynamic link = Shell().CreateShortcut(Link); string target = link.TargetPath; return (string)link.Description == Description && target.StartsWith(install + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && (string)link.Arguments == "--autostart"; }
    internal bool Enabled => Owned();
    internal string InstalledTarget()
    {
        PlannerBackup.SafePath(install);
        if (!File.Exists(Path.Combine(install, ".desktop-calendar-owned")) || File.ReadAllText(Path.Combine(install, ".desktop-calendar-owned")) != "desktop-calendar") throw new InvalidOperationException("StartupUninstalled");
        using var d = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(install, "installation.json"))); var e = d.RootElement;
        if (e.GetProperty("SchemaVersion").GetInt32() != 1 || e.GetProperty("AppId").GetString() != "desktop-calendar") throw new InvalidDataException("UnknownInstallation");
        if (!e.TryGetProperty("Version", out var v) || !Version.TryParse(v.GetString(), out var installed) || installed < new Version(0, 8, 0)) throw new InvalidOperationException("StartupUninstalled");
        string relative = e.GetProperty("Current").GetString()!;
        if (!System.Text.RegularExpressions.Regex.IsMatch(relative, @"^versions\\v[a-f0-9]{16}$")) throw new InvalidDataException("UnknownInstallation");
        string target = Path.Combine(install, relative, "DesktopCalendar.exe"); PlannerBackup.SafePath(target); if (!File.Exists(target)) throw new InvalidOperationException("StartupUninstalled"); return target;
    }
    internal void Set(bool enabled)
    {
        PlannerBackup.SafePath(Link); if (File.Exists(Link) && !Owned()) throw new IOException("ForeignStartupEntry");
        if (!enabled) { if (File.Exists(Link)) File.Delete(Link); return; }
        string target = InstalledTarget(); Directory.CreateDirectory(startup); string temp = Path.Combine(startup, "DesktopCalendar-" + Guid.NewGuid().ToString("N") + ".lnk");
        try { dynamic link = Shell().CreateShortcut(temp); link.TargetPath = target; link.Arguments = "--autostart"; link.WorkingDirectory = Path.GetDirectoryName(target); link.IconLocation = target + ",0"; link.Description = Description; link.Save(); if (File.Exists(Link)) File.Replace(temp, Link, null); else File.Move(temp, Link); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
