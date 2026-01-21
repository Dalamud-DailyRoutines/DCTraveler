using System.Text.Json.Serialization;

namespace DCTravelerX.Infos;

public class MigrationOrder
{
    [JsonPropertyName("orderId")]
    public string OrderId { get; set; }

    [JsonPropertyName("roleId")]
    public string ContentId { get; set; }

    [JsonPropertyName("groupId")]
    public int GroupId { get; set; }

    [JsonPropertyName("groupCode")]
    public string GroupCode { get; set; }

    [JsonPropertyName("groupName")]
    public string GroupName { get; set; }

    [JsonPropertyName("createTime")]
    public string CreateTime { get; set; }
}
