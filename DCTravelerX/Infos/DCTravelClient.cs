using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DCTravelerX.Managers;

namespace DCTravelerX.Infos;

internal class DCTravelClient
{
    private static DCTravelClient? instance { get; set; }
    
    public static List<Area> CachedAreas { get; set; } = [];
    public static bool       IsValid     { get; private set; }
    
    public bool IsDisposed { get; internal set; }
    
    public bool IsUpdatingAllQueryTime { get; private set; }

    private HttpClient httpClient { get; init; }

    private readonly string APIURL;

    private static readonly SemaphoreSlim Lock = new(Environment.ProcessorCount, Environment.ProcessorCount);
    
    public static DCTravelClient Instance(int port = 0)
    {
        if (instance != null) return instance;
        
        return instance ??= new(port);
    }

    private DCTravelClient(int port, bool useEncrypt = true)
    {
        APIURL = $"http://127.0.0.1:{port}/dctravel/";
        
        httpClient = new HttpClient();
        Task.Run(async () =>
        {
            CachedAreas = await QueryGroupListTravelSource();
            IsValid     = true;

            _ = Task.Run(async () => await IntervalQueryAllTravelTime());
        });
    }
    
    internal async Task IntervalQueryAllTravelTime()
    {
        while (true)
        {
            if (IsDisposed) return;

            await QueryAllTravelTime();
            await Task.Delay(600_000);
        }
    }
    
    internal async Task QueryAllTravelTime()
    {
        if (IsUpdatingAllQueryTime || !IsValid || CachedAreas is not { Count: > 0 }) return;
        
        try
        {
            IsUpdatingAllQueryTime = true;

            var tasks = CachedAreas.SelectMany(x => x.GroupList).Select(GetWaitTime).ToList();
            await Task.WhenAll(tasks);
        }
        finally
        {
            IsUpdatingAllQueryTime = false;
        }

        return;

        async Task GetWaitTime(Group group)
        {
            await Lock.WaitAsync();
            
            try
            {
                var waitTime = await QueryTravelQueueTime(group.AreaId, group.GroupId);
                group.QueueTime = waitTime;
                Service.Log.Debug($"获取 {group.GroupName} ({group.AreaName}) 当前等待时间: {waitTime}");
            }
            finally
            {
                Lock.Release();
            }
        }
    }
    
    public async Task<T> RequestApi<T>(object[] objs, [CallerMemberName] string? method = null)
    {
        var rpcRequest  = new RpcRequest { Method = method!, Params = objs };
        var jsonPayload = JsonSerializer.Serialize(rpcRequest);
        Service.Log.Debug($"请求 API: {jsonPayload}");
        
        var request  = new HttpRequestMessage(HttpMethod.Post, APIURL) { Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json") };
        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        Service.Log.Debug($"API 回应: {content}");
        
        var rpcResponse = JsonSerializer.Deserialize<RpcResponse>(content);
        if (rpcResponse?.Error != null) 
            throw new Exception(rpcResponse.Error);
        
        if (rpcResponse!.Result is JsonElement element)
        {
            if (typeof(T) == typeof(string)) 
                return (T)(object)element.GetString()!;

            return element.Deserialize<T>();
        }

        return (T)Convert.ChangeType(rpcResponse.Result, typeof(T));
    }

    public async Task<List<Area>> QueryGroupListTravelSource() => 
        await RequestApi<List<Area>>([]);

    public async Task<List<Area>> QueryGroupListTravelTarget(int areaID, int groupID) => 
        await RequestApi<List<Area>>([areaID, groupID]);

    public async Task<List<Character>> QueryRoleList(int areaId, int groupId) => 
        await RequestApi<List<Character>>([areaId, groupId]);

    public async Task<int> QueryTravelQueueTime(int areaId, int groupId) => 
        await RequestApi<int>([areaId, groupId]);

    public async Task<string> TravelOrder(Group targetGroup, Group sourceGroup, Character character) =>
        await RequestApi<string>([targetGroup, sourceGroup, character]);

    public async Task<OrderStatus> QueryOrderStatus(string orderId) => 
        await RequestApi<OrderStatus>([orderId]);

    public async Task<MigrationOrders> QueryMigrationOrders(int pageIndex = 1) => 
        await RequestApi<MigrationOrders>([pageIndex]);

    public async Task<string> TravelBack(string orderId, int currentGroupId, string currentGroupCode, string currentGroupName) =>
        await RequestApi<string>([orderId, currentGroupId, currentGroupCode, currentGroupName]);

    public async Task<string> RefreshGameSessionId() => 
        await RequestApi<string>([]);

    public async Task MigrationConfirmOrder(string orderId, bool confirmed) => 
        await RequestApi<string>([orderId, confirmed]);

    public async Task SetSdoArea(string name) => await RequestApi<string>([name]);
}
