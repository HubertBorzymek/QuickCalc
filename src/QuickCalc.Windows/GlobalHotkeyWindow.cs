using System.ComponentModel;
using System.Windows.Forms;

namespace QuickCalc.Windows;

[Flags]
public enum HotkeyModifiers : uint { None = 0, Alt = 1, Control = 2, Shift = 4, Win = 8, NoRepeat = 0x4000 }

public sealed class GlobalHotkeyWindow : NativeWindow, IDisposable
{
    private readonly HashSet<int> _registered = [];
    public event EventHandler<int>? Pressed;

    public GlobalHotkeyWindow() => CreateHandle(new CreateParams { Caption = "QuickCalc.Hotkeys" });

    public void Register(int id, Keys key, HotkeyModifiers modifiers)
    {
        if (!NativeMethods.RegisterHotKey(Handle, id, (uint)(modifiers | HotkeyModifiers.NoRepeat), (uint)key))
            throw new Win32Exception($"Nie można zarejestrować skrótu {modifiers}+{key}.");
        _registered.Add(id);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WmHotkey) Pressed?.Invoke(this, m.WParam.ToInt32());
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        foreach (var id in _registered) NativeMethods.UnregisterHotKey(Handle, id);
        _registered.Clear();
        DestroyHandle();
    }
}
