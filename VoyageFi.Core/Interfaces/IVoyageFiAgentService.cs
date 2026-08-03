namespace VoyageFi.Core.Interfaces;

public interface IVoyageFiAgentService
{
    Task<string> ResolveAsync(string prompt, CancellationToken cancellationToken = default);
}
