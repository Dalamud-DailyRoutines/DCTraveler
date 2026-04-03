using System.Threading;
using System.Threading.Tasks;

namespace DCTravelerX.Travel.Services;

internal interface ICooldownGate
{
    Task WaitAsync(CancellationToken cancellationToken);
}
