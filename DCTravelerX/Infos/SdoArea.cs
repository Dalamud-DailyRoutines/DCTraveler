using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DCTravelerX.Infos;

public class SdoArea
{
    // "Areaid":"1",
    public string Areaid { get; set; } = string.Empty;

    // "AreaStat":1,
    public int AreaStat { get; set; }

    // "AreaOrder":4,
    public int AreaOrder { get; set; }

    // "AreaName":"陆行鸟",
    public string AreaName { get; set; } = string.Empty;

    // "Areatype":1,
    public int Areatype { get; set; }

    // "AreaLobby":"ffxivlobby01.ff14.sdo.com",
    public string AreaLobby { get; set; } = string.Empty;

    // "AreaGm":"ffxivgm01.ff14.sdo.com",
    public string AreaGm { get; set; } = string.Empty;

    // "AreaPatch":"ffxivpatch01.ff14.sdo.com",
    public string AreaPatch { get; set; } = string.Empty;

    // "AreaConfigUpload":"ffxivsdb01.ff14.sdo.com"
    public string AreaConfigUpload { get; set; } = string.Empty;

    public static async Task<SdoArea[]> Get()
    {
        var handler = new HttpClientHandler
        {
            UseProxy    = true,
            Proxy       = WebRequest.GetSystemWebProxy(),
            Credentials = CredentialCache.DefaultCredentials
        };

        using var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(30);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://ff.dorado.sdo.com/ff/area/serverlist_new.js");
        request.Headers.Add("Accept", "*/*");
        request.Headers.Add("Host",   "ff.dorado.sdo.com");

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var text = await response.Content.ReadAsStringAsync();
        
        var json = text.Trim();
        json = json["var servers=".Length..];
        json = json[..^1];

        return JsonConvert.DeserializeObject<SdoArea[]>(json);
    }
} 
