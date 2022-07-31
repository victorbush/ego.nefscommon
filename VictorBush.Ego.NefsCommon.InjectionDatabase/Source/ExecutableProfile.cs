// See LICENSE.txt for license information.

namespace VictorBush.Ego.NefsCommon.InjectionDatabase;

public class ExecutableProfile
{
	public uint? ApiVersion { get; init; }
	public string? Md5 { get; init; }
	public string? Game { get; init; }
	public IReadOnlyList<InjectionProfile>? InjectionProfiles { get; init; }
}
