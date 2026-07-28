using System;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Drawers;

public sealed unsafe class Plugin : IDalamudPlugin
{
    private const string CommandName = "/drawers";
    private const int FallbackDelayTicks = 5;
    
    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    internal static IClientState ClientState { get; private set; } = null!;

    [PluginService]
    internal static ICondition Condition { get; private set; } = null!;

    [PluginService]
    internal static IObjectTable ObjectTable { get; private set; } = null!;

    [PluginService]
    internal static IFramework Framework { get; private set; } = null!;

    [PluginService]
    internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog PluginLog { get; private set; } = null!;

    private bool queuedInputMotion;
    private bool? previousWeaponState;
    private bool suppressStateCheck;
    private Hook<InputData.Delegates.IsInputIdPressed> isInputIdPressedHook = null!;

    private Configuration configuration = null!;

    public Plugin()
    {
        configuration =
            PluginInterface.GetPluginConfig() as Configuration
            ?? new Configuration();

        configuration.Initialize(PluginInterface);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage =
                " weapon draw/sheathe\n" +
                "/drawers auto - toggle automatic mode, so that all weapon drawing/sheathing is changed to the emote version"
        });

        isInputIdPressedHook =
            GameInteropProvider.HookFromAddress<InputData.Delegates.IsInputIdPressed>(
                InputData.Addresses.IsInputIdPressed.Value,
                IsInputIdPressedDetour);
        isInputIdPressedHook.Enable();

        Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        isInputIdPressedHook.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    private bool IsInputIdPressedDetour(InputData* thisPtr, InputId inputId)
    {
        var isPressed = false;

        try
        {
            isPressed = isInputIdPressedHook.Original(thisPtr, inputId);
            if (configuration.AutoMode
                && isPressed
                && ClientState.IsLoggedIn
                && !Condition[ConditionFlag.Mounted]
                && IsWeaponToggleInput(inputId))
            {
                QueueInputMotion();
                return false;
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, "Error in IsInputIdPressedDetour.");
        }

        return isPressed;
    }

    private static bool IsWeaponToggleInput(InputId inputId)
    {
        return inputId
            is (InputId.SWARD
            or InputId.NOTARGET_SWORD
            or InputId.PAD_SWARD
            or InputId.PAD_DRAWN_SWORD);
    }

    private void QueueInputMotion()
    {
        if (queuedInputMotion)
            return;

        queuedInputMotion = true;
        Framework.RunOnTick(() =>
        {
            queuedInputMotion = false;
            ExecuteDrawersMotion();
        });
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!configuration.AutoMode || !ClientState.IsLoggedIn)
            return;

        var player = ObjectTable.LocalPlayer;
        if (player == null)
            return;

        bool currentState =
            player.StatusFlags.HasFlag(StatusFlags.WeaponOut);

        if (previousWeaponState == null)
        {
            previousWeaponState = currentState;
            return;
        }

        if (suppressStateCheck)
        {
            suppressStateCheck = false;
            previousWeaponState = currentState;
            return;
        }

        if (currentState != previousWeaponState)
        {
            previousWeaponState = currentState;
            ExecuteGameCommand(
                currentState
                    ? "/draw motion"
                    : "/sheathe motion");
        }
    }

    private void OnCommand(string command, string args)
    {
        if (args.Equals("auto",
                StringComparison.OrdinalIgnoreCase))
        {
            configuration.AutoMode = !configuration.AutoMode;
            configuration.Save();

            ExecuteGameCommand(
                $"/echo Drawers automatic mode: {(configuration.AutoMode ? "ON" : "OFF")}");

            return;
        }
        
        if (!ClientState.IsLoggedIn)
            return;

        var player = ObjectTable.LocalPlayer;
        if (player == null)
            return;

        ExecuteDrawersMotion();
    }

    private void ExecuteDrawersMotion()
    {
        var player = ObjectTable.LocalPlayer;
        if (player == null)
            return;

        bool wasWeaponOut = player.StatusFlags.HasFlag(StatusFlags.WeaponOut);

        suppressStateCheck = true;

        ExecuteGameCommand(
            wasWeaponOut
                ? "/sheathe motion"
                : "/draw motion");

        Framework.RunOnTick(() =>
        {
            var currentPlayer = ObjectTable.LocalPlayer;
            if (currentPlayer == null)
                return;

            bool isWeaponOut =
                currentPlayer.StatusFlags.HasFlag(StatusFlags.WeaponOut);

            if (isWeaponOut == wasWeaponOut)
            {
                ExecuteBattleModeToggle();
            }

        }, delayTicks: FallbackDelayTicks);
    }

    private void ExecuteBattleModeToggle()
    {
        suppressStateCheck = true;
        ExecuteGameCommand("/bm");
    }

    private static void ExecuteGameCommand(string command)
    {
        using var utf8 = new Utf8String();
        utf8.SetString(command);

        UIModule.Instance()->ProcessChatBoxEntry(&utf8);
    }
}
