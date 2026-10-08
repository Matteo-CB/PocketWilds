using PocketWilds.Simulation;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// A stage that must see the game's request before the runner starts it (T38: the grooming measures the played animal's
    /// coat and gives its zones to its game before <see cref="IActivityController.Begin"/>). The presenter calls
    /// <see cref="Prepare"/> right after <see cref="IActivityStageProvider.Acquire"/>, then waits for
    /// <see cref="IActivityStage.Ready"/> as usual. A stage without it is simply not prepared.
    /// </summary>
    public interface IActivityStagePreparation
    {
        void Prepare(in ActivityRequest request);
    }
}
