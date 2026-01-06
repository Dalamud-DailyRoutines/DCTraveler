using System;
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
        Service.PI.GetIpcProvider<bool>("DCTravelerX.IsValid").RegisterFunc(IsValid);
        Service.PI.GetIpcProvider<uint, int>("DCTravelerX.GetWaitTime").RegisterFunc(GetWaitTime);
        Service.PI.GetIpcProvider<Task>("DCTravelerX.QueryAllWaitTime").RegisterFunc(QueryAllWaitTime);
    }

    internal static void Uninit()
    {
        Service.PI.GetIpcProvider<int, int, ulong, bool, string, Task<Exception?>>("DCTravelerX.Travel").UnregisterFunc();
        Service.PI.GetIpcProvider<string, Task<bool>>("DCTravelerX.SelectDCAndLogin").UnregisterFunc();
        Service.PI.GetIpcProvider<string, Task<bool>>("DCTravelerX.GetOrderStatus").UnregisterFunc();
        Service.PI.GetIpcProvider<bool>("DCTravelerX.IsValid").UnregisterFunc();
        Service.PI.GetIpcProvider<uint, int>("DCTravelerX.GetWaitTime").UnregisterFunc();
        Service.PI.GetIpcProvider<Task>("DCTravelerX.QueryAllWaitTime").UnregisterFunc();
    }

    // 是否正常连接超域旅行 API
    private static bool IsValid() => 
        DCTravelClient.IsValid && DCTravelClient.Areas is { Count: > 0 };

    private static async Task<bool> SelectDCAndLogin(string name)
    {
        if (Service.GameGui.GetAddonByName("_TitleMenu") == 0)
            return false;

        var newTicket = await DCTravelClient.Instance().RefreshGameSessionId();

        GameFunctions.ChangeToSdoArea(name);
        GameFunctions.ChangeDEVTestSID(newTicket);
        GameFunctions.CloseWaitAddon();
        GameFunctions.LoginInGame();
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

    // 根据服务器获取时间
    private static int GetWaitTime(uint worldID)
    {
        if (!Service.DataManager.GetExcelSheet<World>().TryGetRow(worldID, out var worldRow) ||
            !DCTravelClient.WorldNameToAreaID.TryGetValue(worldRow.Name.ToString(), out var areaID)) 
            return -1;
        
        var foundGroup = DCTravelClient.Areas[areaID].Groups[worldRow.Name.ToString()];
        return foundGroup.QueueTime ?? -1;
    }

    // 手动触发请求全部等待时间
    private static Task QueryAllWaitTime() =>
        DCTravelClient.Instance().QueryAllTravelTime();
}
