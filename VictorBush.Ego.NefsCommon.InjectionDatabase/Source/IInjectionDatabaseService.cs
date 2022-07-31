// See LICENSE.txt for license information.

namespace VictorBush.Ego.NefsCommon.InjectionDatabase;

public interface IInjectionDatabaseService
{
	Task<InjectionDatabaseVersion?> CheckForDatabaseUpdateAsync(CancellationToken cancellationToken = default);
	Task<ExecutableProfile?> FindExeProfileAsync(string fileName, string md5, CancellationToken cancellationToken = default);
	Task UpdateDatabaseAsync(InjectionDatabaseVersion targetVersion, CancellationToken cancellationToken = default);
}
