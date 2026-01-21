using System.Text.Json.Serialization;

namespace DCTravelerX.Infos;

public class Character
{
    [JsonPropertyName("roleId")]
    public string ContentId { get; set; }

    [JsonPropertyName("roleName")]
    public string Name { get; set; }

    public int AreaId  { get; set; }
    public int GroupId { get; set; }

    public string ToQueryString()
    {
        // Shit!
        return $"{{\"roleId\":\"{ContentId}\",\"roleName\":\"{Name}\",\"key\":0}}";
    }
}
