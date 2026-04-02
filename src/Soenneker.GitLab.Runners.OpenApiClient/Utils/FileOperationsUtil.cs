using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Soenneker.Extensions.String;
using Soenneker.Git.Util.Abstract;
using Soenneker.GitLab.Runners.OpenApiClient.Utils.Abstract;
using Soenneker.Utils.Dotnet.Abstract;
using Soenneker.Utils.Environment;
using Soenneker.Utils.Process.Abstract;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.ValueTask;
using Soenneker.Kiota.Util.Abstract;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.File.Download.Abstract;
using System.Collections.Generic;
using Soenneker.OpenApi.Fixer.Abstract;
using Soenneker.Utils.Yaml.Abstract;

namespace Soenneker.GitLab.Runners.OpenApiClient.Utils;

///<inheritdoc cref="IFileOperationsUtil"/>
public sealed class FileOperationsUtil : IFileOperationsUtil
{
    private readonly ILogger<FileOperationsUtil> _logger;
    private readonly IConfiguration _configuration;
    private readonly IGitUtil _gitUtil;
    private readonly IDotnetUtil _dotnetUtil;
    private readonly IProcessUtil _processUtil;
    private readonly IKiotaUtil _kiotaUtil;
    private readonly IFileDownloadUtil _fileDownloadUtil;
    private readonly IFileUtil _fileUtil;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly IOpenApiFixer _openApiFixer;
    private readonly IYamlUtil _yamlUtil;

    public FileOperationsUtil(ILogger<FileOperationsUtil> logger, IConfiguration configuration, IGitUtil gitUtil, IDotnetUtil dotnetUtil,
        IProcessUtil processUtil, IFileDownloadUtil fileDownloadUtil, IFileUtil fileUtil, IDirectoryUtil directoryUtil, IOpenApiFixer openApiFixer,
        IYamlUtil yamlUtil, IKiotaUtil kiotaUtil)
    {
        _logger = logger;
        _configuration = configuration;
        _gitUtil = gitUtil;
        _dotnetUtil = dotnetUtil;
        _processUtil = processUtil;
        _kiotaUtil = kiotaUtil;
        _fileDownloadUtil = fileDownloadUtil;
        _fileUtil = fileUtil;
        _directoryUtil = directoryUtil;
        _openApiFixer = openApiFixer;
        _yamlUtil = yamlUtil;
    }

    public async ValueTask Process(CancellationToken cancellationToken = default)
    {
        string gitDirectory = await _gitUtil.CloneToTempDirectory($"https://github.com/soenneker/{Constants.Library.ToLowerInvariantFast()}",
            cancellationToken: cancellationToken);

        string yamlPath = Path.Combine(gitDirectory, "openapi.yaml");

        await _fileUtil.DeleteIfExists(yamlPath, cancellationToken: cancellationToken);

        string openApiDocumentUrl = _configuration["GitLab:ClientGenerationUrl"] ??
                                    "https://gitlab.com/gitlab-org/gitlab/-/raw/master/doc/api/openapi/openapi_v3.yaml";

        string? filePath = await _fileDownloadUtil.Download(openApiDocumentUrl, yamlPath, fileExtension: ".yaml", cancellationToken: cancellationToken);

        string jsonPath = Path.Combine(gitDirectory, "openapi.json");

        await _yamlUtil.SaveAsJson(filePath, jsonPath, true, cancellationToken);

        string fixedPath = Path.Combine(gitDirectory, "fixed.json");

        await _openApiFixer.Fix(jsonPath, fixedPath, cancellationToken);

        await _kiotaUtil.EnsureInstalled(cancellationToken);

        string srcDirectory = Path.Combine(gitDirectory, "src", Constants.Library);

        await DeleteAllExceptCsproj(srcDirectory, cancellationToken);

        await _kiotaUtil.Generate(fixedPath, "GitLabOpenApiClient", Constants.Library, gitDirectory, cancellationToken).NoSync();

        await BuildAndPush(gitDirectory, cancellationToken)
            .NoSync();
    }

    public async ValueTask DeleteAllExceptCsproj(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (!(await _directoryUtil.Exists(directoryPath, cancellationToken)))
        {
            _logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
            return;
        }

        try
        {
            // Delete all files except .csproj
            List<string> files = await _directoryUtil.GetFilesByExtension(directoryPath, "", true, cancellationToken);
            foreach (string file in files)
            {
                if (!file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        await _fileUtil.Delete(file, ignoreMissing: true, log: false, cancellationToken);
                        _logger.LogInformation("Deleted file: {FilePath}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete file: {FilePath}", file);
                    }
                }
            }

            // Delete all empty subdirectories
            List<string> dirs = await _directoryUtil.GetAllDirectoriesRecursively(directoryPath, cancellationToken);
            foreach (string dir in dirs.OrderByDescending(d => d.Length)) // Sort by depth to delete from deepest first
            {
                try
                {
                    List<string> dirFiles = await _directoryUtil.GetFilesByExtension(dir, "", false, cancellationToken);
                    List<string> subDirs = await _directoryUtil.GetAllDirectories(dir, cancellationToken);
                    if (dirFiles.Count == 0 && subDirs.Count == 0)
                    {
                        await _directoryUtil.Delete(dir, cancellationToken);
                        _logger.LogInformation("Deleted empty directory: {DirectoryPath}", dir);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete directory: {DirectoryPath}", dir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while cleaning the directory: {DirectoryPath}", directoryPath);
        }
    }

    private async ValueTask BuildAndPush(string gitDirectory, CancellationToken cancellationToken)
    {
        string projFilePath = Path.Combine(gitDirectory, "src", Constants.Library, $"{Constants.Library}.csproj");

        await _dotnetUtil.Restore(projFilePath, cancellationToken: cancellationToken);

        bool successful = await _dotnetUtil.Build(projFilePath, true, "Release", false, cancellationToken: cancellationToken);

        if (!successful)
        {
            _logger.LogError("Build was not successful, exiting...");
            return;
        }

        string gitHubToken = EnvironmentUtil.GetVariableStrict("GH__TOKEN");

        await _gitUtil.CommitAndPush(gitDirectory, "Automated update", gitHubToken, "Jake Soenneker", "jake@soenneker.com", cancellationToken);
    }
}