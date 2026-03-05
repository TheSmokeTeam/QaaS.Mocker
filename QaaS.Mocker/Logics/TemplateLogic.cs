using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Configurations;
using QaaS.Framework.Executions.Logics;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.ExecutionObjects;

namespace QaaS.Mocker.Logics;

/// <summary>
/// Logic class for template command.
/// </summary>
public class TemplateLogic(
    Context context,
    string? templateOutputFolder = null,
    IFileSystem? fileSystem = null,
    TextWriter? writer = null) : ILogic
{
    private const string DefaultTemplateFileName = "template.qaas.yaml";

    private readonly IFileSystem _fileSystem = fileSystem ?? new FileSystem();
    private TextWriter _writer = writer ?? Console.Out;

    public bool ShouldRun(ExecutionType executionType)
    {
        return executionType == ExecutionType.Template;
    }

    /// <summary>
    /// Outputs the configured objects.
    /// </summary>
    public ExecutionData Run(ExecutionData executionData)
    {
        var template = context.RootConfiguration.BuildConfigurationAsYaml(Constants.ConfigurationSectionNames);

        if (!string.IsNullOrWhiteSpace(templateOutputFolder))
        {
            var templatePath = Path.Combine(Environment.CurrentDirectory, templateOutputFolder, DefaultTemplateFileName);
            WriteTemplateToFile(templatePath, template);
            context.Logger.LogInformation("Template written to {TemplatePath}", templatePath);
            return executionData;
        }

        _writer.WriteLine(template);
        return executionData;
    }

    private void WriteTemplateToFile(string filePath, string templateContent)
    {
        var directoryPath = _fileSystem.Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath) && !_fileSystem.Directory.Exists(directoryPath))
            _fileSystem.Directory.CreateDirectory(directoryPath);

        _fileSystem.File.WriteAllText(filePath, templateContent);
    }
}
