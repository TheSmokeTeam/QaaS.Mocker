using System.IO.Abstractions;
using System.Text;
using Autofac;
using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.ContextObjects;
using YamlDotNet.Serialization;

namespace QaaS.Mocker.Executions;

/// <summary>
/// Template execution mode.
/// Template a configuration file to see how it looks after being loaded.
/// Will construct what it can even if the configuration file is invalid.
/// </summary>
public class TemplateExecution(Context context, string? templateOutputFolder) : BaseExecution(context, false)
{
    protected IFileSystem FileSystem = new FileSystem();
    private const string DefaultTemplateFileName = "template", TemplateFileExtension = "qaas.yaml";

    /// <inheritdoc />
    protected override int Execute(ILifetimeScope scope)
    {
        var configurationObjects = Constants.ConfigurationTypes.Select(scope.Resolve).ToList();
        var yamlSerializer = new SerializerBuilder().WithIndentedSequences().Build();
        var stringBuilder = new StringBuilder();
        foreach (var configurationObject in configurationObjects)
            stringBuilder.Append($"{yamlSerializer.Serialize(configurationObject)}\n");


        var template = stringBuilder.ToString();

        if (templateOutputFolder != null)
        {
            WriteValueToFile(template,
                Path.ChangeExtension(
                    Path.Combine(Environment.CurrentDirectory, templateOutputFolder, DefaultTemplateFileName), 
                    TemplateFileExtension)
                );
            return 0;
        }

        Context.Logger.LogInformation("\n{Template}", template);
        return 0;
    }

    private void WriteValueToFile(string value, string filePath)
    {
        if (FileSystem.File.Exists(filePath))
            Context.Logger.LogWarning("{FileToWriteTo} already exists, overriding its content.",
                filePath);
        else
        {
            // Create file's directory if it doesn't exist already
            var fileDirectory = Path.GetDirectoryName(filePath);
            if (fileDirectory != string.Empty && !FileSystem.Directory.Exists(fileDirectory))
                FileSystem.Directory.CreateDirectory(fileDirectory);

            FileSystem.File.Create(filePath).Dispose();
        }

        Context.Logger.LogInformation("Writing template to {FilePath}", filePath);
        FileSystem.File.WriteAllText(filePath, value);
    }
}