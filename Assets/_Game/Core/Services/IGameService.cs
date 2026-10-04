#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace PocketWilds.Core
{
    /// <summary>
    /// Implemented only by services that need asynchronous initialization.
    /// The order is set explicitly by GameBootstrap, never discovered by reflection.
    /// </summary>
    public interface IGameService
    {
        Task InitializeAsync(CancellationToken cancellationToken);
    }
}
