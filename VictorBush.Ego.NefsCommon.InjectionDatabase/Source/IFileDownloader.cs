// See LICENSE.txt for license information.

using System.Net;

namespace VictorBush.Ego.NefsCommon.InjectionDatabase;

public interface IFileDownloader
{
	Task<(HttpStatusCode StatusCode, string FileContent)> DownloadTextFileAsync(string url, CancellationToken cancellationToken);
	Task<(HttpStatusCode StatusCode, byte[] FileContent)> DownloadZipFileAsync(string url, CancellationToken cancellationToken);
}
