using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopCalendar;
// Uses documented Windows DirectWrite/Direct2D/WIC APIs and the installed Segoe UI Emoji font.
// COM calls are isolated here; no third-party font, glyph assets, package or network is involved.
internal static class ColorEmojiRaster
{
    static readonly Dictionary<string, BitmapSource?> cache = new(StringComparer.Ordinal);
    internal static BitmapSource? Get(string text)
    {
        if (cache.TryGetValue(text, out var found)) return found;
        BitmapSource? image;
        try { image = Render(text); }
        catch (Exception e) when (e is COMException or ArgumentException or InvalidOperationException or ExternalException) { image = null; }
        if (cache.Count >= 256) cache.Clear(); cache[text] = image; return image;
    }
    static T Method<T>(IntPtr instance, int slot) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));
    static void Check(int hr) => Marshal.ThrowExceptionForHR(hr);
    static BitmapSource? Render(string text)
    {
        IntPtr wic = IntPtr.Zero, d2d = IntPtr.Zero, write = IntPtr.Zero, bitmap = IntPtr.Zero, target = IntPtr.Zero, format = IntPtr.Zero, brush = IntPtr.Zero;
        try
        {
            var cls = new Guid("cacaf262-9370-4615-a13b-9f5539da4c0a"); var iid = new Guid("ec5ec8a9-c395-4314-9c77-54d7a935ff70");
            Check(CoCreateInstance(ref cls, IntPtr.Zero, 1, ref iid, out wic));
            iid = new Guid("06152247-6f50-465a-9245-118bfd3b6007"); Check(D2D1CreateFactory(0, ref iid, IntPtr.Zero, out d2d));
            iid = new Guid("b859ee5a-d838-4b5b-a2e8-1adc7d93db48"); Check(DWriteCreateFactory(0, ref iid, out write));
            var pixels = new Guid("6fddc324-4e03-4bfe-b185-3d77768dc910"); const int width = 512, height = 128;
            Check(Method<CreateBitmap>(wic, 17)(wic, width, height, ref pixels, 1, out bitmap));
            var props = new TargetProperties { Type = 1, Format = 87, Alpha = 1, DpiX = 96, DpiY = 96 };
            Check(Method<CreateTarget>(d2d, 13)(d2d, bitmap, ref props, out target));
            Check(Method<CreateFormat>(write, 15)(write, "Segoe UI Emoji", IntPtr.Zero, 400, 0, 5, 64, "en-us", out format));
            Check(Method<SetUInt>(format, 5)(format, 1)); // no-wrap
            var white = new Color4 { R = 1, G = 1, B = 1, A = 1 }; Check(Method<CreateBrush>(target, 8)(target, ref white, IntPtr.Zero, out brush));
            Method<VoidCall>(target, 48)(target); var transparent = new Color4(); Method<Clear>(target, 47)(target, ref transparent);
            var rect = new Rect4 { Right = width, Bottom = height };
            // FE0F requests emoji presentation for symbols such as hearts, but never changes stored user text.
            string render = text.Contains('\uFE0F') || char.IsSurrogate(text[0]) ? text : text + "\uFE0F";
            Method<DrawText>(target, 27)(target, render, (uint)render.Length, format, ref rect, brush, 4, 0);
            Check(Method<EndDraw>(target, 49)(target, out _, out _));
            var bytes = new byte[width * height * 4]; Check(Method<CopyPixels>(bitmap, 7)(bitmap, IntPtr.Zero, width * 4, bytes.Length, bytes));
            int left = width, top = height, right = -1, bottom = -1; bool color = false;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int p = (y * width + x) * 4; if (bytes[p + 3] == 0) continue;
                left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                if (Math.Abs(bytes[p] - bytes[p + 2]) > 12 || Math.Abs(bytes[p + 1] - bytes[p + 2]) > 12) color = true;
            }
            if (!color || right < left) return null; // unsupported glyphs keep the original readable WPF fallback
            int w = right - left + 1, h = bottom - top + 1; var cropped = new byte[w * h * 4];
            for (int y = 0; y < h; y++) Buffer.BlockCopy(bytes, ((y + top) * width + left) * 4, cropped, y * w * 4, w * 4);
            var result = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, cropped, w * 4); result.Freeze(); return result;
        }
        finally { foreach (var p in new[] { brush, format, target, bitmap, write, d2d, wic }) if (p != IntPtr.Zero) Marshal.Release(p); }
    }
    [StructLayout(LayoutKind.Sequential)] struct Color4 { public float R, G, B, A; }
    [StructLayout(LayoutKind.Sequential)] struct Rect4 { public float Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct TargetProperties { public uint Type, Format, Alpha; public float DpiX, DpiY; public uint Usage, Level; }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateBitmap(IntPtr self, int width, int height, ref Guid format, int cache, out IntPtr bitmap);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateTarget(IntPtr self, IntPtr bitmap, ref TargetProperties props, out IntPtr target);
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)] delegate int CreateFormat(IntPtr self, string family, IntPtr fonts, uint weight, uint style, uint stretch, float size, string locale, out IntPtr format);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetUInt(IntPtr self, uint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CreateBrush(IntPtr self, ref Color4 color, IntPtr props, out IntPtr brush);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void VoidCall(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void Clear(IntPtr self, ref Color4 color);
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)] delegate void DrawText(IntPtr self, string text, uint length, IntPtr format, ref Rect4 rect, IntPtr brush, uint options, uint measuring);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EndDraw(IntPtr self, out ulong tag1, out ulong tag2);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int CopyPixels(IntPtr self, IntPtr rect, int stride, int size, [Out] byte[] bytes);
    [DllImport("ole32.dll")] static extern int CoCreateInstance(ref Guid cls, IntPtr outer, uint context, ref Guid iid, out IntPtr instance);
    [DllImport("d2d1.dll")] static extern int D2D1CreateFactory(uint type, ref Guid iid, IntPtr options, out IntPtr factory);
    [DllImport("dwrite.dll")] static extern int DWriteCreateFactory(uint type, ref Guid iid, out IntPtr factory);
}
