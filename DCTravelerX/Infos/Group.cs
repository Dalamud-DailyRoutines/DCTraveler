using System.Text.Json.Serialization;

namespace DCTravelerX.Infos;

public class Group
{
    public int    AreaId   { get; set; }
    public string AreaName { get; set; }

    [JsonPropertyName("groupId")]
    public int GroupID { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("groupName")]
    public string GroupName { get; set; }

    [JsonPropertyName("queueTime")]
    public int? QueueTime { get; set; } = -1;

    [JsonPropertyName("groupCode")]
    public string GroupCode { get; set; }
}
