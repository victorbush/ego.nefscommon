// See LICENSE.txt for license information.

using System.Security.Cryptography;

namespace VictorBush.Ego.NefsCommon.InjectionDatabase;

public static class Md5Utility
{
	public static string Compute(string filePath)
	{
		using var stream = File.OpenRead(filePath);
		using var md5 = MD5.Create();

		var hash = md5.ComputeHash(stream);
		return string.Join("", hash.Select(x => x.ToString("X2")));
	}
}
