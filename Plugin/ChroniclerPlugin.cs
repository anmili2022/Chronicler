using Dalamud.Plugin;
using KamiToolKit;
using System.Numerics;

namespace Chronicler;

public sealed partial class ChroniclerPlugin : IDalamudPlugin
{
    private const string CommandName = "/shiguan";
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly CrescentStateService stateService;
    private readonly FateAppearanceDetector appearanceDetector;
    private readonly CriticalEncounterDetector criticalEncounterDetector;
    private readonly CurrencyGainTracker currencyGainTracker;
    private readonly AchievementProgressService achievementProgress;
    private readonly VnavService vnav;
    private readonly CrescentMapMarkerController mapMarkers;
    private readonly PluginUI ui;
    private bool isDisposing;
    private DateTime lastFrameworkErrorUtc = DateTime.MinValue;
    private DateTime lastAutoNavigationUpdateUtc = DateTime.MinValue;
    private string activeAutoNavigationKey = string.Empty;
    private string pendingAutoNavigationKey = string.Empty;
    private DateTime? pendingAutoNavigationDueUtc;
    private bool autoNavigationReturned;
    private DateTime? autoReturnDueUtc;
    private DateTime? pendingStandbyNavUtc;
    private DateTime? pendingStandbyNavStartedUtc;
    private DateTime? pendingStandbyBaseCampUtc;
    private ExpeditionMap? pendingAutoReturnMap;
    private DateTime? pendingAutoReturnStartedUtc;
    private DateTime? pendingAutoReturnBaseCampUtc;
    private bool pendingAutoReturnSawBetweenAreas;
    private int pendingAutoReturnRetryCount;
    private bool wasDead;
    private bool achievementWasDead;
    private Vector3? pendingAchievementRespawnTarget;
    private string pendingAchievementRespawnLabel = string.Empty;
    private DateTime? pendingAchievementRespawnStartedUtc;
    private DateTime lastAchievementRespawnNavigationAttemptUtc = DateTime.MinValue;
    private Vector3? pendingAchievementSelectionPosition;
    private DateTime? pendingAchievementSelectionStartedUtc;
    private Vector3? activeAchievementRespawnTarget;
    private Vector3 activeAchievementRespawnLastPosition;
    private DateTime? activeAchievementRespawnStartedUtc;
    private int activeAchievementRespawnRetryCount;
    private DateTime? postReturnScanDueUtc;
    private bool autoNavWasEnabled;
    private bool autoIslandCycleActive;
    private bool autoIslandReentryStarted;
    private DateTime autoIslandLeaveRequestedUtc = DateTime.MinValue;
    private DateTime? autoIslandLeftUtc;
    private readonly InstancePopulationProvider instancePopulationProvider = new();

    public string Name => "新月岛史官";

    public PluginConfiguration Configuration { get; }

    public ChroniclerPlugin(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        DalamudApi.Initialize(pluginInterface);
        KamiToolKitLibrary.Initialize(pluginInterface, Name);

        Configuration = pluginInterface.GetPluginConfig() as PluginConfiguration ?? new PluginConfiguration();
        Configuration.Initialize(pluginInterface);
        LogHelper.Initialize(Configuration);

        stateService = new CrescentStateService(Configuration);
        appearanceDetector = new FateAppearanceDetector(stateService);
        criticalEncounterDetector = new CriticalEncounterDetector(stateService);
        currencyGainTracker = new CurrencyGainTracker();
        achievementProgress = new AchievementProgressService(Configuration);
        vnav = new VnavService(pluginInterface, Configuration);
        mapMarkers = new CrescentMapMarkerController(Configuration);
        ui = new PluginUI(Configuration, stateService, vnav, currencyGainTracker, instancePopulationProvider, mapMarkers, achievementProgress);

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
        mapMarkers.Dispose();
        vnav.Dispose();
        KamiToolKitLibrary.Dispose();
        Configuration.Save();
    }
}
