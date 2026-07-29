using Dalamud.Plugin;

namespace Chronicler;

public sealed partial class ChroniclerPlugin : IDalamudPlugin
{
    private const string CommandName = "/shiguan";
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly CrescentStateService stateService;
    private readonly FateAppearanceDetector appearanceDetector;
    private readonly CriticalEncounterDetector criticalEncounterDetector;
    private readonly PluginUI ui;
    private bool isDisposing;
    private DateTime lastFrameworkErrorUtc = DateTime.MinValue;

    public string Name => "新月岛史官";

    public PluginConfiguration Configuration { get; }

    public ChroniclerPlugin(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        DalamudApi.Initialize(pluginInterface);

        Configuration = pluginInterface.GetPluginConfig() as PluginConfiguration ?? new PluginConfiguration();
        Configuration.Initialize(pluginInterface);
        LogHelper.Initialize(Configuration);

        stateService = new CrescentStateService(Configuration);
        appearanceDetector = new FateAppearanceDetector(stateService);
        criticalEncounterDetector = new CriticalEncounterDetector(stateService);
        ui = new PluginUI(Configuration, stateService);

        RegisterCommands();
        RegisterChatHandlers();

        pluginInterface.UiBuilder.Draw += ui.Draw;
        pluginInterface.UiBuilder.OpenMainUi += ui.OpenMainWindow;
        pluginInterface.UiBuilder.OpenConfigUi += ui.OpenMainWindow;
        DalamudApi.Framework.Update += OnFrameworkUpdate;
        LogHelper.Info("新月岛史官已加载。");
    }

    public void Dispose()
    {
        isDisposing = true;
        DalamudApi.Framework.Update -= OnFrameworkUpdate;
        pluginInterface.UiBuilder.Draw -= ui.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= ui.OpenMainWindow;
        pluginInterface.UiBuilder.OpenConfigUi -= ui.OpenMainWindow;
        UnregisterChatHandlers();
        UnregisterCommands();
        ui.Dispose();
        Configuration.Save();
    }
}
