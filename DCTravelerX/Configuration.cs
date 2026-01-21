using System;
using Dalamud.Configuration;

namespace DCTravelerX;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public bool EnableAutoRetry   { get; set; } = true;
    public int  MaxRetryCount     { get; set; } = 20;
    public int  RetryDelaySeconds { get; set; } = 60;
    public int  Version           { get; set; } = 1;

    public void Save() =>
        Service.PI.SavePluginConfig(this);
}
