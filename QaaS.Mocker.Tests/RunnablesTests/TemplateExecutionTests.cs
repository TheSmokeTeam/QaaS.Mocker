using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using NUnit.Framework;
using QaaS.Mocker.Executions;
using QaaS.Mocker.Tests.Mocks;

namespace QaaS.Mocker.Tests.RunnablesTests;

public class TemplateExecutionTests
{
    private static readonly MethodInfo? MethodWriteValueToFile =
        typeof(TemplateExecution).GetMethod("WriteValueToFile", BindingFlags.Instance | BindingFlags.NonPublic);
    
    private static IEnumerable<TestCaseData> TestWriteValueToFileFileTestCaseData()
    {
        // ValidValueAndPathOfFileInCurrentDirectory
        yield return new TestCaseData("Value: \nValue", "path.txt"); 

        // NullValueAndValidPath
        yield return new TestCaseData(null, "path.txt"); 
        
        // EmptyValueAndValidPath
        yield return new TestCaseData("", "path.txt"); 
    }
    
    [Test, TestCaseSource(nameof(TestWriteValueToFileFileTestCaseData))]
    public void 
        TestWriteValueToFileFileDoesNotExist_CallFunctionWithPathToFileThatDoesNotExist_ShouldCreateTheFileAndWriteTheCorrectValueToIt
        (string? valueToWrite, string filePath)
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var mockTemplateExecutor = new TemplateExecutionMock(mockFileSystem);
        
        // Act
        MethodWriteValueToFile!.Invoke(mockTemplateExecutor, new object?[] { valueToWrite, filePath });
        
        // Assert
        Assert.That(mockFileSystem.File.Exists(filePath));
        Assert.That(mockFileSystem.File.ReadAllText(filePath), Is.EqualTo(valueToWrite ?? string.Empty));
    }
    
    [Test, TestCaseSource(nameof(TestWriteValueToFileFileTestCaseData))]
    public void 
        TestWriteValueToFileFileDoesExist_CallFunctionWithPathToFileThatDoesExist_ShouldOverrideTheFileContentsWithGivenValue
        (string? valueToWrite, string filePath)
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        mockFileSystem.File.Create(filePath).Dispose();
        mockFileSystem.File.WriteAllText(filePath, $"{valueToWrite}_with_added_data_that_should_be_overwritten");

        var mockTemplateExecutor = new TemplateExecutionMock(mockFileSystem);

        // Act
        MethodWriteValueToFile!.Invoke(mockTemplateExecutor, new object?[] { valueToWrite, filePath });
        
        // Assert
        Assert.That(mockFileSystem.File.Exists(filePath));
        Assert.That(mockFileSystem.File.ReadAllText(filePath), Is.EqualTo(valueToWrite ?? string.Empty));

    }

}