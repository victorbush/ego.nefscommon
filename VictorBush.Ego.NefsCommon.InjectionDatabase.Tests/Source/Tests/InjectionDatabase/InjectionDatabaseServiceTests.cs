// See LICENSE.txt for license information.

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Xunit;

namespace VictorBush.Ego.NefsCommon.InjectionDatabase.Tests;

public sealed class InjectionDatabaseServiceTests
{
	private static readonly string RootDatabaseDirectory = InjectionDatabaseService.RootDatabaseDirectory;
	private static readonly string ServerDbUrl = "https://example.com/db";

	[Fact]
	public async Task CheckForDatabaseUpdate_CurrentVersionEqualToLatest_NullReturned()
	{
		MockCurrentVersionFile(99, 1);
		MockCurrentDatabase(99, 1);
		MockLatestDatabaseVersionFile(HttpStatusCode.OK, 99, 1);

		var service = CreateService();
		var result = await service.CheckForDatabaseUpdateAsync();
		Assert.Null(result);
	}

	[Fact]
	public async Task CheckForDatabaseUpdate_CurrentVersionFoundButNoLatestVersion_NullReturned()
	{
		MockCurrentVersionFile(99, 1);
		MockCurrentDatabase(99, 1);
		MockLatestDatabaseVersionFile(HttpStatusCode.NotFound, null, null);

		var service = CreateService();
		var result = await service.CheckForDatabaseUpdateAsync();
		Assert.Null(result);
	}

	[Fact]
	public async Task CheckForDatabaseUpdate_CurrentVersionGreaterThanLatest_NullReturned()
	{
		MockCurrentVersionFile(99, 1);
		MockCurrentDatabase(99, 1);
		MockLatestDatabaseVersionFile(HttpStatusCode.OK, 99, 1);

		var service = CreateService();
		var result = await service.CheckForDatabaseUpdateAsync();
		Assert.Null(result);
	}

	[Fact]
	public async Task CheckForDatabaseUpdate_CurrentVersionLessThanLatest_LatestVersionReturned()
	{
		MockCurrentVersionFile(50, 1);
		MockCurrentDatabase(50, 1);
		MockLatestDatabaseVersionFile(HttpStatusCode.OK, 99, 1);

		var service = CreateService();
		var result = await service.CheckForDatabaseUpdateAsync();
		Assert.Equal<uint>(1, result.ApiVersion.Value);
		Assert.Equal<uint>(99, result.DbVersion.Value);
	}

	[Fact]
	public async Task CheckForDatabaseUpdate_NoCurrentVersionAButLatestVersionFound_LatestVersionReturned()
	{
		MockLatestDatabaseVersionFile(HttpStatusCode.OK, 99, 1);

		var service = CreateService();
		var result = await service.CheckForDatabaseUpdateAsync();
		Assert.Equal<uint>(1, result.ApiVersion.Value);
		Assert.Equal<uint>(99, result.DbVersion.Value);
	}

	[Fact]
	public async Task CheckForDatabaseUpdate_NoCurrentVersionAndNoLatestVersionFound_NullReturned()
	{
		MockLatestDatabaseVersionFile(HttpStatusCode.NotFound, null, null);

		var service = CreateService();
		var result = await service.CheckForDatabaseUpdateAsync();
		Assert.Null(result);
	}

	[Fact]
	public async Task CheckForDatabaseUpdate_UpdateAvailableButApiVersionOutOfDate_NullReturned()
	{
		MockCurrentVersionFile(50, 1);
		MockCurrentDatabase(50, 1);
		MockLatestDatabaseVersionFile(HttpStatusCode.OK, 99, 2);

		var service = CreateService();
		var result = await service.CheckForDatabaseUpdateAsync();
		Assert.Null(result);
	}

	[Fact]
	public async Task FindExeProfile_Found_Returned()
	{
		MockCurrentVersionFile(50, 1);
		MockCurrentDatabase(50, 1);

		var service = CreateService();
		var result = await service.FindExeProfileAsync(ValidExeName, ValidMd5);
		Assert.Equal("test-game", result.Game);
		Assert.Equal(ValidMd5, result.Md5);
	}

	[Fact]
	public async Task FindExeProfile_NotFound_NullReturned()
	{
		MockCurrentVersionFile(50, 1);
		MockCurrentDatabase(50, 1);

		var service = CreateService();
		var result = await service.FindExeProfileAsync("invalid.exe", ValidMd5);
		Assert.Null(result);
	}

	[Fact]
	public async Task UpdateDatabaseAsync_ValidDb_DownloadedAndExtracted()
	{
		MockCurrentVersionFile(50, 1);
		MockCurrentDatabase(50, 1);
		MockLatestDatabaseVersionFile(HttpStatusCode.OK, 99, 1);
		MockLatestDatabase(HttpStatusCode.OK, 99, 1);

		var service = CreateService();
		await service.UpdateDatabaseAsync(new InjectionDatabaseVersion
		{
			ApiVersion = 1,
			DbVersion = 99,
		});

		// Verify files on disk
		var dbDirectory = Path.Combine(RootDatabaseDirectory, "99");
		var versionFile = Path.Combine(dbDirectory, "version.json");
		Assert.True(this.fileSystem.FileExists(versionFile));

		var exeProfilePath = Path.Combine(dbDirectory, "ExeProfiles", $"{ValidExeName}.{ValidMd5}.json");
		Assert.True(this.fileSystem.FileExists(exeProfilePath));

		// Verify current version marker was updated
		var currentFileContent = this.fileSystem.File.ReadAllText(InjectionDatabaseService.CurrentVersionFilePath);
		var currentVersion = JsonSerializer.Deserialize<InjectionDatabaseVersion>(currentFileContent);
		Assert.Equal<uint>(InjectionDatabaseService.ApiVersion, currentVersion.ApiVersion!.Value);
		Assert.Equal<uint>(99, currentVersion.DbVersion!.Value);
	}

	private InjectionDatabaseService CreateService()
	{
		return new InjectionDatabaseService(
			new NullLogger<InjectionDatabaseService>(),
			this.fileDownloaderMock.Object,
			this.fileSystem,
			new InjectionDatabaseServiceSettings
			{
				SourceServerDbPath = ServerDbUrl,
			});
	}

	private void MockCurrentVersionFile(uint? dbVersion, uint? apiVersion)
	{
		var currentVersionFilePath = InjectionDatabaseService.CurrentVersionFilePath;
		this.fileSystem.AddFile(currentVersionFilePath, JsonSerializer.Serialize(new InjectionDatabaseVersion
		{
			DbVersion = dbVersion,
			ApiVersion = apiVersion,
		}));
	}

	private void MockCurrentDatabase(uint? dbVersion, uint? apiVersion)
	{
		var dbDirectory = Path.Combine(RootDatabaseDirectory, dbVersion.ToString());
		var versionFile = Path.Combine(dbDirectory, "version.json");
		this.fileSystem.AddFile(versionFile, JsonSerializer.Serialize(new InjectionDatabaseVersion
		{
			ApiVersion = apiVersion,
			DbVersion = dbVersion,
		}));

		var exeProfileDir = Path.Combine(dbDirectory, "ExeProfiles");
		var validExeProfilePath = Path.Combine(exeProfileDir, $"{ValidExeName}.{ValidMd5}.json");
		this.fileSystem.AddFile(validExeProfilePath, JsonSerializer.Serialize(new ExecutableProfile
		{
			ApiVersion = InjectionDatabaseService.ApiVersion,
			Game = "test-game",
			Md5 = ValidMd5,
		}));
	}

	private void MockLatestDatabase(HttpStatusCode code, uint? dbVersion, uint? apiVersion)
	{
		var dbZipFileUrl = ServerDbUrl + "/" + dbVersion.ToString() + ".zip";

		var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		var tempDbDir = Path.Combine(tempDir, "db");
		Directory.CreateDirectory(tempDbDir);

		try
		{
			File.WriteAllText(Path.Combine(tempDbDir, "version.json"), JsonSerializer.Serialize(new InjectionDatabaseVersion
			{
				ApiVersion = apiVersion,
				DbVersion = dbVersion,
			}));

			Directory.CreateDirectory(Path.Combine(tempDbDir, "ExeProfiles"));
			File.WriteAllText(Path.Combine(tempDbDir, "ExeProfiles", $"{ValidExeName}.{ValidMd5}.json"), JsonSerializer.Serialize(new ExecutableProfile
			{
				ApiVersion = InjectionDatabaseService.ApiVersion,
				Game = "test-game",
				Md5 = ValidMd5,
			}));

			ZipFile.CreateFromDirectory(tempDbDir, Path.Combine(tempDir, "zip.zip"));
			var bytes = File.ReadAllBytes(Path.Combine(tempDir, "zip.zip"));

			this.fileDownloaderMock.Setup(x => x.DownloadZipFileAsync(dbZipFileUrl, CancellationToken.None))
				.ReturnsAsync((code, bytes));
		}
		finally
		{
			if (Directory.Exists(tempDir))
			{
				Directory.Delete(tempDir, true);
			}
		}
	}

	private void MockLatestDatabaseVersionFile(HttpStatusCode code, uint? dbVersion, uint? apiVersion)
	{
		var latestFileUrl = ServerDbUrl + "/latest.json";
		this.fileDownloaderMock.Setup(x => x.DownloadTextFileAsync(latestFileUrl, CancellationToken.None))
			.ReturnsAsync((code, JsonSerializer.Serialize(new InjectionDatabaseVersion
			{
				ApiVersion = apiVersion,
				DbVersion = dbVersion,
			})));
	}

	private const string ValidExeName = "valid.exe";
	private const string ValidMd5 = "52fbf080a8760e5bd9f2341781785c78";

	private readonly Mock<IFileDownloader> fileDownloaderMock = new();
	private readonly MockFileSystem fileSystem = new();
}
