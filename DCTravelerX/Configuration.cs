using System;
using Dalamud.Configuration;

namespace DCTravelerX;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    
    public bool EnableAutoRetry { get; set; } = true;
    public int  MaxRetryCount   { get; set; } = 1000;

    public void Save() =>
        Service.PI.SavePluginConfig(this);
}
