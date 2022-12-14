// See LICENSE.txt for license information.

namespace VictorBush.Ego.NefsCommon.InjectionDatabase;

public class InjectionProfile
{
	public string? DataFile { get; init; }
	public ulong? PrimaryOffset { get; init; }
	public ulong? PrimarySize { get; init; }
	public ulong? SecondaryOffset { get; init; }
	public ulong? SecondarySize { get; init; }

	public ulong? RvaOfPrimarySizeToUpdate { get; init; }
	public ulong? RvaOfSecondarySizeToUpdate { get; init; }
	public ulong? RvaOfInstructionForPrimary { get; init; }
	public ulong? RvaOfInstructionForSecondary { get; init; }
}
