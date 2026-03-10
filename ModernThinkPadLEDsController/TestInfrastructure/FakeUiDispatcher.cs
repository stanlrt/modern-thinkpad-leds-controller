using ModernThinkPadLEDsController.Shell;

namespace ModernThinkPadLEDsController.TestInfrastructure;

// TODO: Temporary fake IUiDispatcher for deterministic tests.
//       Replace with a mock-framework stub once a mocking library is adopted.

/// <summary>
/// Executes dispatched actions synchronously and records the dispatch count.
/// </summary>
internal sealed class FakeUiDispatcher : IUiDispatcher
{
    private int _dispatchCount;

    /// <summary>Number of times <see cref="Dispatch"/> was called.</summary>
    public int DispatchCount => _dispatchCount;

    /// <inheritdoc />
    public void Dispatch(Action action)
    {
        _dispatchCount++;
        action();
    }

    /// <summary>Reset the dispatch counter.</summary>
    public void Reset() => _dispatchCount = 0;
}
