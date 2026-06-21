using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace Drawers;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool AutoMode { get; set; } = false;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public void Save()
    {
        pluginInterface?.SavePluginConfig(this);
    }
}
