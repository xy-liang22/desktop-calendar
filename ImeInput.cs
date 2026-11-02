using System;
using System.Windows;
using System.Windows.Input;

namespace DesktopCalendar;

// Composition belongs to the input context, not to the user's global IME setting.
internal sealed class ImeInput
{
    internal bool Composing { get; private set; }
    internal event Action? Completed;
    internal ImeInput(UIElement target)
    {
        TextCompositionManager.AddPreviewTextInputStartHandler(target, (_, _) => Composing = true);
        TextCompositionManager.AddPreviewTextInputUpdateHandler(target, (_, _) => Composing = true);
        TextCompositionManager.AddPreviewTextInputHandler(target, (_, _) => { Composing = false; Completed?.Invoke(); });
    }
    internal bool Consumes(KeyEventArgs e) => Composing || e.Key is Key.ImeProcessed or Key.DeadCharProcessed;
}
