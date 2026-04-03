using System.Threading;
using System.Threading.Tasks;
using DCTravelerX.Travel.Models;
using DCTravelerX.Windows.MessageBox;

namespace DCTravelerX.Travel.Interaction;

internal interface ITravelInteraction
{
    Task PrepareSessionAsync(CancellationToken cancellationToken);

    Task BeginSubmissionAsync(TravelResolution resolution, CancellationToken cancellationToken);

    Task ShowWaitAsync(string message, CancellationToken cancellationToken = default);

    Task CloseWaitAsync(CancellationToken cancellationToken = default);

    Task<MessageBoxResult> ShowMessageAsync(string title, string message, MessageBoxType type = MessageBoxType.Ok, bool showWebsite = false);

    Task<bool> ConfirmMigrationAsync(string targetDcGroupName, bool isIpcCall);

    Task ResetTitleIdleTimeAsync();

    Task SelectDcAndLoginAsync(string targetDcGroupName, bool enterGame);

    Task CleanupSessionAsync(bool needReLogin);

    void CleanupImmediately();

    void PlayCompletionSound();
}
