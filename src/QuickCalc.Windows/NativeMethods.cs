using System.Runtime.InteropServices;
using System.Text;

namespace QuickCalc.Windows;

internal static class NativeMethods
{
    internal const int WmHotkey = 0x0312;
    internal const int EmGetSel = 0x00B0;
    internal const int EmSetSel = 0x00B1;
    internal const int EmReplaceSel = 0x00C2;
    internal const int WmGetText = 0x000D;
    internal const int WmGetTextLength = 0x000E;
    internal const uint KeyeventfUnicode = 0x0004;
    internal const uint KeyeventfKeyup = 0x0002;
    internal const int InputKeyboard = 1;

    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo info);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] internal static extern bool IsWindowEnabled(IntPtr hWnd);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] internal static extern IntPtr SetFocus(IntPtr hWnd);
    [DllImport("user32.dll")] internal static extern bool AttachThreadInput(uint attach, uint attachTo, bool value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, ref int wParam, ref int lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, StringBuilder lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, Input[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)] internal struct GuiThreadInfo
    {
        internal int cbSize; internal uint flags; internal IntPtr hwndActive; internal IntPtr hwndFocus;
        internal IntPtr hwndCapture; internal IntPtr hwndMenuOwner; internal IntPtr hwndMoveSize; internal IntPtr hwndCaret;
        internal System.Drawing.Rectangle rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)] internal struct Input { internal int type; internal InputUnion union; }
    [StructLayout(LayoutKind.Explicit)] internal struct InputUnion { [FieldOffset(0)] internal KeyboardInput keyboard; }
    [StructLayout(LayoutKind.Sequential)] internal struct KeyboardInput
    {
        internal ushort virtualKey; internal ushort scanCode; internal uint flags; internal uint time; internal UIntPtr extraInfo;
    }
}
