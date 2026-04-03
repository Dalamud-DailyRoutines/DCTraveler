using System;
using System.Threading;
using System.Threading.Tasks;
using DCTravelerX.Travel.Interaction;
using DCTravelerX.Travel.Runtime;

namespace DCTravelerX.Travel.Services;

internal sealed class TravelCooldownGate
(
    ITravelInteraction interaction
) : ICooldownGate
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        var remaining = TravelRuntime.GetCooldownRemaining(Cooldown);
        if (remaining <= TimeSpan.Zero)
            return;

        Service.Log.Info($"传送请求过于频繁, 等待 {remaining.TotalSeconds} 秒");

        var waitStart = DateTime.UtcNow;

        while (DateTime.UtcNow - waitStart < remaining)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentRemaining = remaining - (DateTime.UtcNow - waitStart);
            if (currentRemaining < TimeSpan.Zero)
                currentRemaining = TimeSpan.Zero;

            await interaction.ShowWaitAsync($"传送请求过于频繁\n等待 {currentRemaining.TotalSeconds:F0} 秒后自动继续……", cancellationToken);
            await Task.Delay(500, cancellationToken);
        }

        await interaction.CloseWaitAsync(cancellationToken);
    }
}
