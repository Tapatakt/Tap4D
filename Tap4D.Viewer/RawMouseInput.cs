using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Tap4D.Viewer;

/// <summary>
/// Subscribes to raw mouse input (WM_INPUT): relative movement deltas straight from the OS,
/// without cursor warping. Immune to the event-queue races of synthetic cursor moves.
/// </summary>
internal sealed class RawMouseInput
{
    private const int WmInput = 0x00FF;

    private readonly Action<int, int> _onDelta;

    /// <summary>
    /// Registers the window for raw mouse input.
    /// </summary>
    /// <param name="window">The window to hook.</param>
    /// <param name="onDelta">Called with relative mouse deltas (in raw mouse counts).</param>
    public RawMouseInput(Window window, Action<int, int> onDelta)
    {
        _onDelta = onDelta;
        HwndSource source = (HwndSource)PresentationSource.FromVisual(window)!;
        source.AddHook(WndProc);
        RawInputDevice[] devices = [new() { UsagePage = 0x01, Usage = 0x02, Flags = 0, Target = source.Handle }];
        RegisterRawInputDevices(devices, 1, Marshal.SizeOf<RawInputDevice>());
    }

    /// <summary>
    /// Handles WM_INPUT messages: extracts the relative mouse delta.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmInput)
            return IntPtr.Zero;
        int size = Marshal.SizeOf<RawInput>();
        GetRawInputData(lParam, 0x10000003, out RawInput input, ref size, Marshal.SizeOf<RawInputHeader>());
        if (input.Header.Type == 0 && (input.Mouse.LastX != 0 || input.Mouse.LastY != 0))
            _onDelta(input.Mouse.LastX, input.Mouse.LastY);
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public int Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public int Type;
        public int Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawMouse
    {
        public ushort Flags;
        public ushort ButtonFlags;
        public ushort ButtonData;
        public uint RawButtons;
        public int LastX;
        public int LastY;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInput
    {
        public RawInputHeader Header;
        public RawMouse Mouse;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, int count, int size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetRawInputData(IntPtr hRawInput, int command, out RawInput data, ref int size, int headerSize);
}
