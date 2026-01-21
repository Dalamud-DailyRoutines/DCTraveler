using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DCTravelerX.Helpers;
using DCTravelerX.Infos;

namespace DCTravelerX.Managers;

public static class ServerDataManager
{
    public static SdoArea[]? SdoAreas { get; private set; }

    internal static void Init()
    {
        Task.Run
        (() =>
            {
                try
                {
                    var hostInfoString    = GameFunctions.GetGameArgument("XL.LobbyHosts");
                    var decodedBytes      = Convert.FromBase64String(hostInfoString);
                    var decodedJsonString = Encoding.UTF8.GetString(decodedBytes);
                    SdoAreas = JsonSerializer.Deserialize<SdoArea[]>(decodedJsonString);

                    Service.Log.Information($"从游戏参数获取到 {SdoAreas.Length} 个大区主机信息");
                }
                catch (Exception ex)
                {
                    Service.Log.Error("获取服务器大区数据失败", ex);
                }
            }
        );
    }

    internal static void Uninit() =>
        SdoAreas = null;
}
