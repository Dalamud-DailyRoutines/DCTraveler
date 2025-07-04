using System.Threading.Tasks;
using DCTravelerX.Helpers;
using DCTravelerX.Infos;

namespace DCTravelerX.Managers;

public static class IpcManager
{
    internal static void Init()
    {
        //旅行
        Service.PI.GetIpcProvider<int, int, ulong, string, Task<string>>("DCTravelerX.Travel")
               .RegisterFunc(TravelManager.TravelIpc);
        //返回
        Service.PI.GetIpcProvider<int, ulong, Task<string>>("DCTravelerX.TravelBack")
               .RegisterFunc(TravelManager.TravelBackIpc);
        //换区
        Service.PI.GetIpcProvider<string, Task<string>>("DCTravelerX.SelectDCAndLogin")
               .RegisterFunc(SelectDcAndLoginIpc);
        Service.PI.GetIpcProvider<string, Task<bool>>("DCTravelerX.GetorderStatus")
               .RegisterFunc(TravelManager.GetorderStatus);
    }

    internal static void Uninit()
    {
        Service.PI.GetIpcProvider<int, int, ulong, string, object>("DCTravelerX.Travel")
               .RegisterFunc(TravelManager.TravelIpc);
        Service.PI.GetIpcProvider<int, ulong, Task<string>>("DCTravelerX.TravelBack")
               .RegisterFunc(TravelManager.TravelBackIpc);
        Service.PI.GetIpcProvider<string, Task<string>>("DCTravelerX.SelectDCAndLogin")
               .RegisterFunc(SelectDcAndLoginIpc);
        Service.PI.GetIpcProvider<string, Task<bool>>("DCTravelerX.GetorderStatus")
               .RegisterFunc(TravelManager.GetorderStatus);
    }


    public static async Task<string> SelectDcAndLoginIpc(string name)
    {
        if (Service.GameGui.GetAddonByName("_TitleMenu") == 0)
        {
            return "不在标题不能换区";
        }

        await Plugin.SelectDCAndLogin(name);
        return "成功";
    }
}
