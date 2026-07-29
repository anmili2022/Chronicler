using Dalamud.Interface.Windowing;

namespace Chronicler;

internal sealed class PluginUI : IDisposable
{
    private readonly WindowSystem windowSystem = new("Chronicler");
    private readonly MainWindow mainWindow;
    private readonly FloatingStatusWindow floatingStatusWindow;

    public PluginUI(PluginConfiguration config, CrescentStateService state, VnavService vnav)
    {
        mainWindow = new MainWindow(config, state, vnav);
        floatingStatusWindow = new FloatingStatusWindow(config, ToggleMainWindow, vnav);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(floatingStatusWindow);
    }

    public void Draw()
    {
        floatingStatusWindow.IsOpen = floatingStatusWindow.ShouldBeOpen;
        windowSystem.Draw();
    }

    public void ToggleMainWindow() => mainWindow.IsOpen = !mainWindow.IsOpen;
    public void OpenMainWindow() => mainWindow.IsOpen = true;

    public void Dispose() => windowSystem.RemoveAllWindows();
}
