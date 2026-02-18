using Microsoft.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace Wysg.Musm.BooHill;

public sealed partial class InfoWindow : Window
{
    public InfoWindow()
    {
        InitializeComponent();
        SetFixedSize(460, 560);
    }

    private void SetFixedSize(int width, int height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(width, height));
    }
}
