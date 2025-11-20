namespace DCTravelerX.Infos;

public class SelectWorldResult
{
    public Group Source { get; set; }
    public Group Target { get; set; }
    public bool EnableRetry { get; set; }
    public int RetryCount { get; set; }
}
