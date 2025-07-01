using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DCTraveler.Infos;

public class Area
{
    [JsonPropertyName("areaId")]
    public int AreaId { get; set; }

    [JsonPropertyName("areaName")]
    public string AreaName { get; set; }

    [JsonPropertyName("groups")]
    public List<Group> GroupList { get; set; }

    public void SetAreaForGroup()
    {
        foreach (var group in GroupList)
        {
            group.AreaName = AreaName;
            group.AreaId   = AreaId;
        }
    }
} 
