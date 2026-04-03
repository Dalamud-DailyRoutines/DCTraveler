using System;
using System.Threading;
using System.Threading.Tasks;

namespace DCTravelerX.Travel.Runtime;

internal static class TravelRuntime
{
    private static readonly SemaphoreSlim TravelGate = new(1, 1);

    private static CancellationTokenSource? activeSessionCancellation;
    private static long                     lastTravelTicks;
    private static long                     lastCancelTicks;
    private static int                      isTravelling;

    public static bool CanRefreshTravelTime => TravelGate.CurrentCount > 0;

    public static bool IsTravelling => Volatile.Read(ref isTravelling) != 0;

    public static CancellationTokenSource RegisterSession(CancellationTokenSource sessionCancellation)
    {
        var previousSession = Interlocked.Exchange(ref activeSessionCancellation, sessionCancellation);
        previousSession?.Cancel();
        return sessionCancellation;
    }

    public static Task WaitAsync(CancellationToken cancellationToken) =>
        TravelGate.WaitAsync(cancellationToken);

    public static void MarkTravelStarted() =>
        Volatile.Write(ref isTravelling, 1);

    public static void MarkTravelFinished() =>
        Volatile.Write(ref isTravelling, 0);

    public static void MarkTravelRequested() =>
        Interlocked.Exchange(ref lastTravelTicks, DateTime.UtcNow.Ticks);

    public static void MarkCancelled() =>
        Interlocked.Exchange(ref lastCancelTicks, DateTime.UtcNow.Ticks);

    public static TimeSpan GetCooldownRemaining(TimeSpan cooldown)
    {
        var lastTravel = Interlocked.Read(ref lastTravelTicks);
        var lastCancel = Interlocked.Read(ref lastCancelTicks);
        var lastAction = Math.Max(lastTravel, lastCancel);

        if (lastAction <= 0)
            return TimeSpan.Zero;

        var elapsed = DateTime.UtcNow - new DateTime(lastAction, DateTimeKind.Utc);
        return elapsed >= cooldown ? TimeSpan.Zero : cooldown - elapsed;
    }

    public static void CompleteSession(CancellationTokenSource sessionCancellation, bool acquiredGate)
    {
        if (ReferenceEquals(Interlocked.CompareExchange(ref activeSessionCancellation, null, sessionCancellation), sessionCancellation))
            sessionCancellation.Cancel();

        MarkTravelFinished();

        if (acquiredGate)
            TravelGate.Release();
    }

    public static void CancelActiveSession()
    {
        var activeSession = Interlocked.Exchange(ref activeSessionCancellation, null);
        activeSession?.Cancel();
        MarkTravelFinished();
    }
}
