using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using DesktopCalendar.Core;

namespace DesktopCalendar;

// Never follows arbitrary Markdown paths. Files are content-addressed, validated PNGs.
// Orphans are retained: Undo/Redo, backup JSON and other notes may still reference them.
internal sealed class LocalImages(string root, Func<PlannerState?> state)
{
    internal const int MaxBytes = 20 * 1024 * 1024;
    internal readonly List<LocalAsset> Pending = new();
    internal IEnumerable<LocalAsset> Assets => (state()?.Assets.AsEnumerable() ?? []).Concat(Pending).DistinctBy(a => a.Id);
    static readonly Regex References = new(@"(?:assets/images/[A-Za-z0-9._-]+\.(?:png|jpg|jpeg|bmp|gif|webp)|asset://[0-9a-fA-F-]{36})", RegexOptions.IgnoreCase);
    internal Guid[] Referenced(string text) => References.Matches(text).Cast<Match>().Select(m => Find(m.Value)?.Id).Where(id => id != null).Select(id => id!.Value).Distinct().ToArray();
    internal LocalAsset? Find(string reference) => Assets.FirstOrDefault(a => reference == "asset://" + a.Id.ToString("D") || reference == a.RelativePath || reference == "./" + a.RelativePath);
    string SafePath(string relative)
    {
        if (!Regex.IsMatch(relative, @"^assets/images/[A-Za-z0-9._-]+\.(png|jpg|jpeg|bmp|gif|webp)$", RegexOptions.IgnoreCase)) throw new IOException("UnsafeImagePath");
        var basePath = Path.GetFullPath(root); var path = Path.GetFullPath(Path.Combine(basePath, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("UnsafeImagePath");
        for (var part = new FileInfo(path) as FileSystemInfo; part != null; part = part is FileInfo f ? f.Directory : ((DirectoryInfo)part).Parent)
        { if (part.Exists && (part.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("ReparseImagePath"); }
        return path;
    }
    static BitmapSource Decode(byte[] bytes)
    {
        if (bytes.Length > MaxBytes) throw new IOException("ImageTooLarge");
        using var header = new MemoryStream(bytes); var probe = BitmapDecoder.Create(header, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand); Validate(probe.Frames[0]);
        using var stream = new MemoryStream(bytes); var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0]; Validate(frame); frame.Freeze(); return frame;
    }
    static void Validate(BitmapSource source)
    { if (source.PixelWidth <= 0 || source.PixelHeight <= 0 || source.PixelWidth > 8192 || source.PixelHeight > 8192 || (long)source.PixelWidth * source.PixelHeight > 24000000) throw new IOException("ImageTooLarge"); }
    internal LocalAsset ImportFile(string path)
    {
        if (new FileInfo(path).Length > MaxBytes) throw new IOException("ImageTooLarge");
        return Import(Decode(File.ReadAllBytes(path)));
    }
    internal LocalAsset Import(BitmapSource source)
    {
        Validate(source); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source)); using var buffer = new MemoryStream(); encoder.Save(buffer);
        byte[] bytes = buffer.ToArray(); if (bytes.Length > MaxBytes) throw new IOException("ImageTooLarge");
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(); string relative = "assets/images/" + hash + ".png";
        var path = SafePath(relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); path = SafePath(relative);
        if (File.Exists(path)) { if (!SHA256.HashData(File.ReadAllBytes(path)).SequenceEqual(SHA256.HashData(bytes))) throw new IOException("ImageHashMismatch"); }
        else
        {
            var temporary = path + ".pending-" + Guid.NewGuid().ToString("N");
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes); file.Flush(true); }
            _ = Decode(File.ReadAllBytes(temporary));
            try { File.Move(temporary, path); } catch (IOException) when (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(bytes)) { File.Delete(temporary); }
        }
        var asset = Assets.FirstOrDefault(a => a.Sha256 == hash && a.RelativePath == relative);
        if (asset == null) { asset = new() { Id = Guid.NewGuid(), RelativePath = relative, MediaType = "image/png", Sha256 = hash, Length = bytes.Length }; Pending.Add(asset); }
        return asset;
    }
    internal BitmapSource? Read(string reference)
    {
        try
        {
            var asset = Find(reference); if (asset == null) return null; var path = SafePath(asset.RelativePath);
            if (!File.Exists(path) || new FileInfo(path).Length != asset.Length || asset.Length > MaxBytes) return null;
            var bytes = File.ReadAllBytes(path); if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase)) return null;
            return Decode(bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.IO.FileFormatException) { return null; }
    }
}
