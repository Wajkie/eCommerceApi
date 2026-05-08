namespace eCommerceApi.Services;

using eCommerceApi.Models;

public interface IAttackDetectionService
{
    Task<bool> CheckForDDoSAsync(string clientIp);

    Task<Alert?> CheckForAnomaliesAsync();

    void RecordRequest(string clientIp);

    Task BlockIpAsync(string clientIp);

    Task UnblockIpAsync(string clientIp);

    Task<IEnumerable<string>> GetBlockedIpsAsync();
}
