using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DCTravelerX.Helpers;
using DCTravelerX.Infos;
using Lumina.Excel.Sheets;

namespace DCTravelerX.Managers;

public static class IpcManager
{
    internal static void Init()
    {
        //旅行
        Service.PI.GetIpcProvider<int, int, ulong, bool, string, Task<string>>("DCTravelerX.Travel")
               .RegisterFunc(Travel);
        //换区
        Service.PI.GetIpcProvider<string, Task<bool>>("DCTravelerX.SelectDCAndLogin")
               .RegisterFunc(SelectDcAndLogin);
        //获取订单状态 
        Service.PI.GetIpcProvider<string, Task<bool>>("DCTravelerX.GetorderStatus")
               .RegisterFunc(GetOrderStatus);
    }

    internal static void Uninit()
    {
        Service.PI.GetIpcProvider<int, int, ulong, bool, string, object>("DCTravelerX.Travel")
               .RegisterFunc(Travel);
        Service.PI.GetIpcProvider<string, Task<bool>>("DCTravelerX.SelectDCAndLogin")
               .RegisterFunc(SelectDcAndLogin);
        Service.PI.GetIpcProvider<string, Task<bool>>("DCTravelerX.GetorderStatus")
               .RegisterFunc(GetOrderStatus);
    }


    private static async Task<bool> SelectDcAndLogin(string name)
    {
        if (Service.GameGui.GetAddonByName("_TitleMenu") == 0)
        {
            return false;
        }

        var newTicket = await DCTravelClient.Instance().RefreshGameSessionId();

        Plugin.ChangeToSdoArea(name);
        GameFunctions.ChangeDevTestSid(newTicket);
        return true;
    }

    //传送获取一个订单号
    private static async Task<string> Travel(
        int currentWorldId, int targetWorldId, ulong contentId, bool isBack, string currentCharacterName)
    {
        var orderID = string.Empty;
        try
        {
            if (Plugin.LastErrorMessage != null)
            {
                return orderID;
            }

            if (DCTravelClient.Instance() is not { IsValid: true } instance)
            {
                Service.Log.Error("无法连接至 XIVLauncherCN 提供的超域旅行 API 服务");
                return orderID;
            }

            var worldSheet = Service.DataManager.GetExcelSheet<World>();
            var currentWorldname = worldSheet.GetRow((uint)currentWorldId).Name.ExtractText();
            var targetWorldname = worldSheet.GetRow((uint)targetWorldId).Name.ExtractText();
            var areas = await instance.QueryGroupListTravelTarget(7, 5); //获取全部大区信息
            var isGetSourceGroup = TryGetGroup(areas, currentWorldname, out var Source);

            if (isBack && isGetSourceGroup)
            {
                var order = await TravelManager.GetTravelingOrder(contentId);
                orderID = await instance.TravelBack(order.OrderId, Source.GroupId, Source.GroupCode,
                                                    Source.GroupName);
                return orderID;
            }

            var isGetTargetGroup = TryGetGroup(areas, targetWorldname, out var Target);
            if (isGetSourceGroup && isGetTargetGroup)
            {
                var chara = new Character { ContentId = contentId.ToString(), Name = currentCharacterName };
                var waitTime =
                    await instance.QueryTravelQueueTime(Target.AreaId,
                                                        Target.GroupId);
                orderID = await instance.TravelOrder(Target, Source, chara);
                return orderID;
            }

            return orderID;
        }
        catch (Exception e)
        {
            Service.Log.Error(e.Message);
            return orderID;
        }
    }


    private static bool TryGetGroup(List<Area> areas, string worldname, out Group t)
    {
        var matchedGroup = areas.SelectMany(area => area.GroupList)
                                .FirstOrDefault(group => group.GroupName == worldname);

        t = matchedGroup ?? new Group();
        return matchedGroup != null;
    }

    //获取订单状态的
    private static async Task<bool> GetOrderStatus(string orderId)
    {
        while (true)
        {
            var status = await DCTravelClient.Instance().QueryOrderStatus(orderId);
            switch (status.Status)
            {
                case MigrationStatus.Completed:
                    return true;
                case MigrationStatus.TeleportFailed or MigrationStatus.PreCheckFailed:
                    return false;
                case MigrationStatus.NeedConfirm:
                {
                    await DCTravelClient.Instance().MigrationConfirmOrder(orderId, true);
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
