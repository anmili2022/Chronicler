using Dalamud.Interface.Windowing;

namespace Chronicler;

internal sealed class PluginUI : IDisposable
{
    private readonly WindowSystem windowSystem = new("Chronicler");
    private readonly MainWindow mainWindow;
    private readonly FloatingStatusWindow floatingStatusWindow;
    private readonly MapMarkerSwitcherWindow mapMarkerSwitcherWindow;

    public PluginUI(PluginConfiguration config, CrescentStateService state, VnavService vnav, CurrencyGainTracker currencyGainTracker, InstancePopulationProvider populationProvider, CrescentMapMarkerController mapMarkers, AchievementProgressService achievementProgress)
    {
        mainWindow = new MainWindow(config, state, vnav, populationProvider, mapMarkers, achievementProgress);
        floatingStatusWindow = new FloatingStatusWindow(config, state, ToggleMainWindow, vnav, currencyGainTracker, achievementProgress);
        mapMarkerSwitcherWindow = new MapMarkerSwitcherWindow(config, mapMarkers);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(floatingStatusWindow);
        windowSystem.AddWindow(mapMarkerSwitcherWindow);
    }

    public void Draw()
    {
        floatingStatusWindow.IsOpen = floatingStatusWindow.ShouldBeOpen;
        mapMarkerSwitcherWindow.UpdateVisibility();
        windowSystem.Draw();
    }

    public void ToggleMainWindow() => mainWindow.IsOpen = !mainWindow.IsOpen;
    public void OpenMainWindow() => mainWindow.IsOpen = true;

    public void Dispose() => windowSystem.RemoveAllWindows();
}
