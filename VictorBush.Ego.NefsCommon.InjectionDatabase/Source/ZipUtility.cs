// See LICENSE.txt for license information.

using System.IO.Abstractions;
using System.IO.Compression;

namespace VictorBush.Ego.NefsCommon.InjectionDatabase;

public static class ZipUtility
{
	public static async Task ExtractZipToDirectoryAsync(this IFileSystem fs, string sourceZipFile, string outputDirectory)
	{
		using var fileStream = fs.File.OpenRead(sourceZipFile);
		using var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read);

		foreach (var entry in zipArchive.Entries)
		{
			var filePath = Path.Combine(outputDirectory, entry.FullName);
			var dir = Path.GetDirectoryName(filePath);
			if (!fs.Directory.Exists(dir))
			{
				fs.Directory.CreateDirectory(dir);
			}

			using var outputStream = fs.File.OpenWrite(filePath);
			using var entryStream = entry.Open();
			await entryStream.CopyToAsync(outputStream);
		}
	}
}
