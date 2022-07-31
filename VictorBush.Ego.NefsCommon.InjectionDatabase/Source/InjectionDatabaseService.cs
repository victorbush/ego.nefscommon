using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using System.Net;
using System.Text.Json;

namespace VictorBush.Ego.NefsCommon.InjectionDatabase;

public sealed class InjectionDatabaseService : IInjectionDatabaseService
{
	public const uint ApiVersion = 1;

	public static readonly string AppDirectory = AppDomain.CurrentDomain.BaseDirectory;
	public static readonly string RootDatabaseDirectory = Path.Combine(AppDirectory, "InjectionDatabase");
	public static readonly string CurrentVersionFilePath = Path.Combine(AppDirectory, RootDatabaseDirectory, "current.json");
	public static readonly string LatestVersionFileRelativePath = "/latest.json";

	private string GetDatabaseDirectory(uint version) => Path.Combine(RootDatabaseDirectory, version.ToString());
	private string GetDatabaseVersionFilePath(uint version) => Path.Combine(GetDatabaseDirectory(version), "version.json");
	private string GetExeProfileFilePath(uint dbVersion, string exeName, string md5) => Path.Combine(GetDatabaseDirectory(dbVersion), "ExeProfiles", $"{exeName}.{md5}.json");
	private string GetRemoteDatabaseZipUrl(uint version) => this.settings.SourceServerDbPath.Trim('/') + "/" + version.ToString() + ".zip";

	private readonly IFileDownloader fileDownloader;
	private readonly IFileSystem fileSystem;
	private readonly ILogger<InjectionDatabaseService> logger;
	private readonly InjectionDatabaseServiceSettings settings;

	private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
	{
		AllowTrailingCommas = true,
		PropertyNameCaseInsensitive = true,
	};

	public InjectionDatabaseService(
		ILogger<InjectionDatabaseService> logger,
		IFileDownloader fileDownloader,
		IFileSystem fileSystem,
		InjectionDatabaseServiceSettings? settings = null)
	{
		this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
		this.fileDownloader = fileDownloader ?? throw new ArgumentNullException(nameof(fileDownloader));
		this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
		this.settings = settings ?? new InjectionDatabaseServiceSettings();
	}

	public async Task<InjectionDatabaseVersion?> CheckForDatabaseUpdateAsync(CancellationToken cancellationToken = default)
	{
		// Check current version
		var currentDatabaseVersion = await GetCurrentDatabaseVersionAsync(cancellationToken);

		// Fetch latest remote version
		var latestVersionFileUrl = this.settings.SourceServerDbPath.TrimEnd('/') + LatestVersionFileRelativePath;
		var latestVersionFileDownload = await this.fileDownloader.DownloadTextFileAsync(latestVersionFileUrl, cancellationToken);
		if (latestVersionFileDownload.StatusCode != HttpStatusCode.OK)
		{
			this.logger.LogError("Failed to get latest version from server: " + latestVersionFileUrl);
			return null;
		}

		var latestVersion = JsonSerializer.Deserialize<InjectionDatabaseVersion>(latestVersionFileDownload.FileContent, JsonSerializerOptions);
		if (latestVersion?.DbVersion is null || latestVersion?.ApiVersion is null)
		{
			this.logger.LogError("Failed to read latest version file.");
			return null;
		}

		// If not a newer version, don't update
		if (currentDatabaseVersion?.DbVersion >= latestVersion.DbVersion)
		{
			this.logger.LogDebug($"Injection database already up-to-date. (current: {currentDatabaseVersion.DbVersion}, latest: {latestVersion.DbVersion})");
			return null;
		}

		// If app version needs updated, don't update database
		if (currentDatabaseVersion?.ApiVersion < latestVersion.ApiVersion)
		{
			this.logger.LogWarning($"Latest injection database requires a software update.");
			return null;
		}

		return latestVersion;
	}

	public async Task<ExecutableProfile?> FindExeProfileAsync(string fileName, string md5, CancellationToken cancellationToken = default)
	{
		var currentDatabaseVersion = await GetCurrentDatabaseVersionAsync(cancellationToken);
		if (currentDatabaseVersion is null)
		{
			return null;
		}

		if (currentDatabaseVersion.DbVersion is null)
		{
			this.logger.LogError("Current database version marker has no database version specified.");
			return null;
		}

		var profilePath = GetExeProfileFilePath(currentDatabaseVersion.DbVersion.Value, fileName, md5);
		if (!this.fileSystem.File.Exists(profilePath))
		{
			this.logger.LogDebug("Exe profile not found: " + profilePath);
			return null;
		}

		var fileContent = await this.fileSystem.File.ReadAllTextAsync(profilePath, cancellationToken);
		return JsonSerializer.Deserialize<ExecutableProfile>(fileContent, JsonSerializerOptions);
	}

	public async Task UpdateDatabaseAsync(InjectionDatabaseVersion targetVersion, CancellationToken cancellationToken = default)
	{
		if (targetVersion.DbVersion is null)
		{
			throw new ArgumentException($"{nameof(targetVersion.DbVersion)} is required.", nameof(targetVersion));
		}

		var version = targetVersion.DbVersion.Value;

		// Download from source server path
		var dbZipUrl = GetRemoteDatabaseZipUrl(version);
		var download = await this.fileDownloader.DownloadZipFileAsync(dbZipUrl, cancellationToken);
		if (download.StatusCode != HttpStatusCode.OK)
		{
			this.logger.LogError("Failed to download database zip: " + dbZipUrl);
			return;
		}

		// Save file to disk and unzip
		var zipFilePath = Path.Combine(RootDatabaseDirectory, targetVersion.DbVersion.ToString() + ".zip");
		if (!this.fileSystem.Directory.Exists(GetDatabaseDirectory(version)))
		{
			this.fileSystem.Directory.CreateDirectory(GetDatabaseDirectory(version));
		}

		await this.fileSystem.File.WriteAllBytesAsync(zipFilePath, download.FileContent, cancellationToken);
		await this.fileSystem.ExtractZipToDirectoryAsync(zipFilePath, GetDatabaseDirectory(version));

		// Update current version marker
		this.fileSystem.File.Copy(GetDatabaseVersionFilePath(version), CurrentVersionFilePath, overwrite: true);
	}

	private async Task<InjectionDatabaseVersion?> GetCurrentDatabaseVersionAsync(CancellationToken cancellationToken)
	{
		if (!this.fileSystem.File.Exists(CurrentVersionFilePath))
		{
			this.logger.LogDebug("Current database version marker not found: " + CurrentVersionFilePath);
			return null;
		}

		var versionFileText = await this.fileSystem.File.ReadAllTextAsync(CurrentVersionFilePath, cancellationToken);
		return JsonSerializer.Deserialize<InjectionDatabaseVersion>(versionFileText, JsonSerializerOptions);
	}
}
