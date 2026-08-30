using System.Reflection;

namespace K7.Clients.MAUI.Controls.Video;

/// <summary>
/// Pointer cursor + hover fill matching TV / keyboard chrome focus.
/// </summary>
internal static class NativeOverlayHover
{
    public static readonly Color Highlight = Color.FromArgb("#66FFFFFF");

#if WINDOWS
    private static readonly PropertyInfo? ProtectedCursorProperty =
        typeof(Microsoft.UI.Xaml.UIElement).GetProperty(
            "ProtectedCursor",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#endif

    public static void Attach(View element, Action<bool>? hoveredChanged = null)
    {
        ApplyHandCursor(element);

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => hoveredChanged?.Invoke(true);
        pointer.PointerExited += (_, _) => hoveredChanged?.Invoke(false);
        element.GestureRecognizers.Add(pointer);
    }

    public static void ApplyHandCursor(VisualElement element)
    {
        void Apply()
        {
#if WINDOWS
            try
            {
                if (element.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement ui
                    && ProtectedCursorProperty is not null)
                {
                    ProtectedCursorProperty.SetValue(
                        ui,
                        Microsoft.UI.Input.InputSystemCursor.Create(
                            Microsoft.UI.Input.InputSystemCursorShape.Hand));
                }
            }
            catch (Exception)
            {
                // WinUI cursor is optional; hover fill still applies.
            }
#endif
        }

        Apply();
        element.HandlerChanged += (_, _) => Apply();
    }
}
