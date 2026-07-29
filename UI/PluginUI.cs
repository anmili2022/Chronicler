using Dalamud.Interface.Windowing;

namespace Chronicler;

internal sealed class PluginUI : IDisposable
{
    private readonly WindowSystem windowSystem = new("Chronicler");
    private readonly MainWindow mainWindow;
    private readonly FloatingStatusWindow floatingStatusWindow;

    public PluginUI(PluginConfiguration config, CrescentStateService state)
    {
        mainWindow = new MainWindow(config, state);
        floatingStatusWindow = new FloatingStatusWindow(config, OpenMainWindow);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(floatingStatusWindow);
    }

    public void Draw()
    {
        floatingStatusWindow.IsOpen = floatingStatusWindow.ShouldBeOpen;
        windowSystem.Draw();
    }

    public void OpenMainWindow() => mainWindow.IsOpen = true;

    public void Dispose() => windowSystem.RemoveAllWindows();
}
