using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DCTravelerX.Infos;
using DCTravelerX.Travel.Interaction;
using DCTravelerX.Travel.Models;

namespace DCTravelerX.Travel.Services;

internal sealed class TravelOrderMonitor
(
    ITravelInteraction interaction
) : IOrderMonitor
{
    private static readonly IReadOnlyDictionary<MigrationStatus, string> StatusText = new Dictionary<MigrationStatus, string>
    {
        [MigrationStatus.TeleportFailed] = "超域旅行传送失败",
        [MigrationStatus.PreCheckFailed] = "超域旅行预检查失败",
        [MigrationStatus.InPrepare0]     = "检查目标大区角色信息中...",
        [MigrationStatus.InPrepare1]     = "检查目标大区角色信息中...",
        [MigrationStatus.NeedConfirm]    = "需要确认传送",
        [MigrationStatus.Processing3]    = "超域旅行排队中...",
        [MigrationStatus.Processing4]    = "超域旅行排队中...",
        [MigrationStatus.Completed]      = "超域旅行完成"
    };

    public async Task WaitForCompletionAsync(string orderId, OrderMonitorOptions options, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var status = await DCTravelClient.Instance().QueryOrderStatus(orderId);
            Service.Log.Information($"当前订单状态: {status.Status}");

            if (options.ShowStatus)
            {
                await interaction.ResetTitleIdleTimeAsync();
                await interaction.ShowWaitAsync(StatusText.GetValueOrDefault(status.Status, "未知状态"), cancellationToken);
            }

            switch (status.Status)
            {
                case MigrationStatus.Completed:
                    if (options.LoginAfterCompletion && !string.IsNullOrWhiteSpace(options.TargetDcGroupName))
                        await interaction.SelectDcAndLoginAsync(options.TargetDcGroupName, !options.IsIpcCall);

                    if (options.PlayCompletionSound)
                        interaction.PlayCompletionSound();

                    return;

                case MigrationStatus.TeleportFailed:
                case MigrationStatus.PreCheckFailed:
                    throw new Exception($"传送失败: {status.CheckMessage} {status.MigrationMessage}");

                case MigrationStatus.NeedConfirm:
                {
                    options.OnStateChanged?.Invoke(TravelState.AwaitingConfirmation);
                    var confirmed = await interaction.ConfirmMigrationAsync(options.TargetDcGroupName ?? string.Empty, options.IsIpcCall);
                    await DCTravelClient.Instance().MigrationConfirmOrder(orderId, confirmed);

                    if (!confirmed)
                        throw new Exception("传送失败: 已自行取消");

                    options.OnStateChanged?.Invoke(TravelState.WaitingOrderStatus);
                    continue;
                }

                case MigrationStatus.InPrepare0:
                case MigrationStatus.InPrepare1:
                case MigrationStatus.Processing3:
                case MigrationStatus.Processing4:
                    await Task.Delay(2_000, cancellationToken);
                    continue;
            }
        }
    }
}
