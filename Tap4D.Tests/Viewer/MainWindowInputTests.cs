using System.Numerics;
using System.Windows.Input;
using System.Windows.Interop;
using Tap4D.Viewer;

namespace Tap4D.Tests.Viewer;

/// <summary>
/// Integration tests for <see cref="MainWindow"/>: real WPF key events on a real window.
/// </summary>
public class MainWindowInputTests
{
    /// <summary>
    /// Raises W keydown on a real MainWindow, pumps frames and checks that the camera moves.
    /// </summary>
    [Fact]
    public void KeyWOnRealWindowMovesCamera()
    {
        Exception? error = null;
        Vector4 position = default;
        Thread sta = new(() =>
        {
            try
            {
                MainWindow window = new();
                using HwndSource source = new(new HwndSourceParameters());
                KeyEventArgs down = new(Keyboard.PrimaryDevice, source, 0, Key.W) { RoutedEvent = Keyboard.KeyDownEvent };
                for (int i = 0; i < 5; i++)
                {
                    window.RaiseEvent(down);
                    window.OnRender(null, EventArgs.Empty);
                }
                position = window.CameraPositionForTest;
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        sta.SetApartmentState(ApartmentState.STA);
        sta.Start();
        sta.Join();
        Assert.Null(error);
        Assert.True(position.Z > -3.9f, $"Expected forward movement from Z=-4, got {position}");
    }
}
