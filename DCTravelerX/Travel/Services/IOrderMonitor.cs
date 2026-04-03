using System.Threading;
using System.Threading.Tasks;
using DCTravelerX.Travel.Models;

namespace DCTravelerX.Travel.Services;

internal interface IOrderMonitor
{
    Task WaitForCompletionAsync(string orderId, OrderMonitorOptions options, CancellationToken cancellationToken);
}
