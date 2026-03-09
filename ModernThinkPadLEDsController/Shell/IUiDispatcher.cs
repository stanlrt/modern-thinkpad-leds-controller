namespace ModernThinkPadLEDsController.Shell;

/// <summary>
/// Abstracts UI-thread dispatch so that coordinators can be tested without a live dispatcher.
/// </summary>
public interface IUiDispatcher
{
    void Dispatch(Action action);
}
