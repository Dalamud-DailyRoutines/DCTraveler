using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DCTravelerX.Helpers;
using DCTravelerX.Infos;
using Lumina.Excel.Sheets;

namespace DCTravelerX.Managers;

public static class IPCManager
{
    internal static void Init()
    {
        Service.PI.GetIpcProvider<int, int, ulong, bool, string, Task<Exception?>>("DCTravelerX.Travel").RegisterFunc(Travel);
        Service.PI.GetIpcProvider<string, Task<bool>>("DCTravelerX.SelectDCAndLogin").RegisterFunc(SelectDCAndLogin);
        Service.PI.GetIpcProvider<string, Task<bool>>("DCTravelerX.GetOrderStatus").RegisterFunc(GetOrderStatus);
    }

    internal static void Uninit()
    {
        Service.PI.GetIpcProvider<int, int, ulong, bool, string, Task<Exception?>>("DCTravelerX.Travel").UnregisterFunc();
        Service.PI.GetIpcProvider<string, Task<bool>>("DCTravelerX.SelectDCAndLogin").UnregisterFunc();
        Service.PI.GetIpcProvider<string, Task<bool>>("DCTravelerX.GetOrderStatus").UnregisterFunc();
    }
    
    private static async Task<bool> SelectDCAndLogin(string name)
    {
        if (Service.GameGui.GetAddonByName("_TitleMenu") == 0)
            return false;

        var newTicket = await DCTravelClient.Instance().RefreshGameSessionId();

        GameFunctions.ChangeToSdoArea(name);
        GameFunctions.ChangeDevTestSid(newTicket);
        return true;
    }

    // 传送获取一个订单号
    private static async Task<Exception?> Travel(int currentWorldId, int targetWorldId, ulong contentId, bool isBack, string currentCharacterName)
    {
        try
        {
            await TravelManager.ExecuteTravelFlow(targetWorldId, currentWorldId, contentId, isBack, false, currentCharacterName, true);
        }
        catch (Exception e)
        {
            Service.Log.Error(e.Message);
            return e;
        }

        return null;
    }

    // 获取订单状态
    private static async Task<bool> GetOrderStatus(string orderID)
    {
        while (true)
        {
            var status = await DCTravelClient.Instance().QueryOrderStatus(orderID);
            
            switch (status.Status)
            {
                case MigrationStatus.Completed:
                    return true;
                case MigrationStatus.TeleportFailed or MigrationStatus.PreCheckFailed:
                    return false;
                case MigrationStatus.NeedConfirm:
                {
                    await DCTravelClient.Instance().MigrationConfirmOrder(orderID, true);
                    continue;
                }
            }

            if (status.Status is not (MigrationStatus.InPrepare0 or MigrationStatus.InPrepare1 or
                MigrationStatus.Processing3 or MigrationStatus.Processing4))
                continue;
            
            await Task.Delay(2000);
        }
    }
}
