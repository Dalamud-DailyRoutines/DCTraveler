namespace DCTravelerX.Infos;

public class RpcRequest
{
    public required string   Method { get; set; }
    public required object[] Params { get; set; }
} 
