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
            CachedAreas = await QueryGroupListTravelTarget(9, 5);
            IsValid     = true;

            _ = Task.Run(async () => await IntervalQueryAllTravelTime());
        });
    }
    
    internal async Task IntervalQueryAllTravelTime()
    {
        while (true)
        {
            if (IsDisposed) return;

            if (TravelManager.TravelSemaphore.CurrentCount > 0)
                await QueryAllTravelTime();
            await Task.Delay(60_000);
        }
    }
    
    internal async Task QueryAllTravelTime()
    {
        if (IsUpdatingAllQueryTime || !IsValid || CachedAreas is not { Count: > 0 }) return;
        
        try
        {
            IsUpdatingAllQueryTime = true;

            CachedAreas = await QueryGroupListTravelTarget(9, 5);
        }
        finally
        {
            IsUpdatingAllQueryTime = false;
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

    // 拂晓可能觉得没用所以删掉/回炉改造了？据反馈也不是很准。目前 25/10/11 在官网也抓不到这个请求了
    public async Task<int> QueryTravelQueueTime(int areaID, int groupID)
    {
        try
        {
            var result = await QueryGroupListTravelTarget(areaID, groupID);
            if (result is not { Count: > 0 })
                return 0;

            if (result.SelectMany(x => x.GroupList).FirstOrDefault(x => x.GroupId == groupID) is not { } group)
                return 0;
            
            return group.QueueTime ?? 0;
        }
        catch
        {
            return 0;
        }
    }

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
