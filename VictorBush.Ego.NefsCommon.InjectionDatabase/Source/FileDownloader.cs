// See LICENSE.txt for license information.

using System.Net;

namespace VictorBush.Ego.NefsCommon.InjectionDatabase;

public sealed class FileDownloader : IFileDownloader
{
	public async Task<(HttpStatusCode StatusCode, string FileContent)> DownloadTextFileAsync(string url, CancellationToken cancellationToken)
	{
		using var client = new HttpClient();
		var response = await client.GetAsync(url, cancellationToken);
		if (response.StatusCode != HttpStatusCode.OK)
		{
			return (response.StatusCode, "");
		}

		var fileContent = await response.Content.ReadAsStringAsync(cancellationToken);
		return (response.StatusCode, fileContent);
	}

	public async Task<(HttpStatusCode StatusCode, byte[] FileContent)> DownloadZipFileAsync(string url, CancellationToken cancellationToken)
	{
		using var client = new HttpClient();
		var response = await client.GetAsync(url, cancellationToken);
		if (response.StatusCode != HttpStatusCode.OK)
		{
			return (response.StatusCode, Array.Empty<byte>());
		}

		var fileContent = await response.Content.ReadAsByteArrayAsync(cancellationToken);
		return (response.StatusCode, fileContent);
	}
}
