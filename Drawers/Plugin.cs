using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Drawers;

public sealed unsafe class Plugin : IDalamudPlugin
{

    private const string CommandName = "/drawers";

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    internal static IClientState ClientState { get; private set; } = null!;

    [PluginService]
    internal static IObjectTable ObjectTable { get; private set; } = null!;

    [PluginService]
    internal static IFramework Framework { get; private set; } = null!;

    public Plugin()
    {
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle weapon draw/sheathe"
        });
        
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // Not needed, but it's probably good practice?
        if (!ClientState.IsLoggedIn)
            return;

        var player = ObjectTable.LocalPlayer;
        if (player == null)
            return;
        
        // Check if weapon is out
        bool wasWeaponOut = player.StatusFlags.HasFlag(StatusFlags.WeaponOut);

        // Emote
        ExecuteGameCommand(wasWeaponOut ? "/sheathe motion" : "/draw motion");
       
        Framework.RunOnTick(() =>
        {
            var currentPlayer = ObjectTable.LocalPlayer;
            if (currentPlayer == null)
                return;
            
            // If it worked
            bool isWeaponOut = currentPlayer.StatusFlags.HasFlag(StatusFlags.WeaponOut);
            

            // If it didn't work
            if (isWeaponOut == wasWeaponOut)
            {
                ExecuteGameCommand("/bm");
            }

        }, delayTicks: 30);
    }

    private static void ExecuteGameCommand(string command)
    {
        using var utf8 = new Utf8String();
        utf8.SetString(command);
        
        UIModule.Instance()->ProcessChatBoxEntry(&utf8);
    }
}
