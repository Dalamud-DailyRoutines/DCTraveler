using Dalamud.Configuration;
using System;

namespace DCTravelerX;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool EnableAutoRetry { get; set; } = false;

    public int MaxRetryCount { get; set; } = 20;

    public int RetryDelaySeconds { get; set; } = 60;

    public void Save()
    {
        Service.PI.SavePluginConfig(this);
    }
}
