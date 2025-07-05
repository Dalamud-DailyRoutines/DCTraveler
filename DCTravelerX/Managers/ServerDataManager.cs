using System;
using System.Threading.Tasks;
using DCTravelerX.Infos;

namespace DCTravelerX.Managers;

public static class ServerDataManager
{
    public static SdoArea[]? SdoAreas { get; private set; }
    
    internal static void Init()
    {
        Task.Run(async () => 
        {
            try
            {
                SdoAreas = await SdoArea.Get();
            }
            catch (Exception ex)
            {
                Service.Log.Error("获取服务器大区数据失败", ex);
            }
        });
    }
    
    internal static void Uninit() => 
        SdoAreas = null;
} 
