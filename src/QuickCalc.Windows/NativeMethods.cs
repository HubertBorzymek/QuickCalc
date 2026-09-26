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
    internal const ushort VkControl = 0x11;
    internal const ushort VkShift = 0x10;
    internal const ushort VkC = 0x43;
    internal const ushort VkV = 0x56;
    internal const ushort VkLeft = 0x25;

    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo info);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] internal static extern bool IsWindowEnabled(IntPtr hWnd);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] internal static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] internal static extern IntPtr SetActiveWindow(IntPtr hWnd);
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
    [StructLayout(LayoutKind.Sequential)] internal struct Rect
    {
        internal int left; internal int top; internal int right; internal int bottom;
    }

    [StructLayout(LayoutKind.Sequential)] internal struct Input { internal int type; internal InputUnion union; }
    [StructLayout(LayoutKind.Explicit)] internal struct InputUnion
    {
        [FieldOffset(0)] internal KeyboardInput keyboard;
        // The largest native union member keeps INPUT at 40 bytes on x64
        // (28 on x86), which is the cbSize required by SendInput.
        [FieldOffset(0)] internal MouseInput mouse;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct KeyboardInput
    {
        internal ushort virtualKey; internal ushort scanCode; internal uint flags; internal uint time; internal UIntPtr extraInfo;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct MouseInput
    {
        internal int dx; internal int dy; internal uint mouseData; internal uint flags; internal uint time; internal UIntPtr extraInfo;
    }
}
