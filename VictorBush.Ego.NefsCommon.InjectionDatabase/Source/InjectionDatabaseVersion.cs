// See LICENSE.txt for license information.

namespace VictorBush.Ego.NefsCommon.InjectionDatabase;

public class InjectionDatabaseVersion
{
	/// <summary>
	/// Database version. Incremented when a new set of profiles is released.
	/// </summary>
	public uint? DbVersion { get; init; }

	/// <summary>
	/// A version number to track application support. If a database format change is required
	/// such that it breaks backwards compatibility with older app versions, this number will increment.
	/// </summary>
	public uint? ApiVersion { get; init; }
}
