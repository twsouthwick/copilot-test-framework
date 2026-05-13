using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System.ComponentModel;

namespace CopilotMockProvider;

public static class FileSystemExtensions
{
    extension(ICopilotMockBuilder builder)
    {
        public ICopilotMockBuilder AddFileSystem(IFileProvider? provider = null)
        {
            var fs = new InMemorySessionFsProvider(provider);

            builder.Services.AddSingleton<IFileProvider>(fs);

            builder
                .AddSystemMessage(
                    $"""
                    CRITICAL INSTRUCTION — File System Access Protocol:

                    You have access to the following file system tools and MUST use them
                    for ALL file system operations:

                      - {nameof(fs.ListFilesTool)}: List files and directories at a given path.
                      - {nameof(fs.ReadFileTool)}: Read the text content of a file.
                      - {nameof(fs.WriteFileTool)}: Write or overwrite a file with given content.
                      - {nameof(fs.DeleteTool)}: Delete a file or directory.
                      - {nameof(fs.RenameTool)}: Rename or move a file or directory.

                    You MUST NOT access the file system through any other means — no code
                    execution, shell commands, inline scripts, or any mechanism other than
                    the tools listed above.

                    If you need a file system operation that is not covered by these tools,
                    you MUST call {nameof(ReportMissingFileSystemOperation)} to report the gap. Do not attempt to
                    work around the limitation.
                    """)
                .AddTool(AIFunctionFactory.Create(fs.ListFilesTool))
                .AddTool(AIFunctionFactory.Create(fs.ReadFileTool))
                .AddTool(AIFunctionFactory.Create(fs.WriteFileTool))
                .AddTool(AIFunctionFactory.Create(fs.DeleteTool))
                .AddTool(AIFunctionFactory.Create(fs.RenameTool))
                .AddTool(AIFunctionFactory.Create(ReportMissingFileSystemOperation));

            return builder;
        }
    }

    extension(IFileProvider provider)
    {
        public string GetFileContents(string path)
        {
            var fileInfo = provider.GetFileInfo(path);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("File not found: " + path);
            }
            using var stream = fileInfo.CreateReadStream();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    [Description("Report a file system operation that is not available through the provided tools. Call this instead of attempting workarounds.")]
    private static string ReportMissingFileSystemOperation(
        [Description("The name of the operation that was needed, e.g. 'Copy', 'Search', 'GetFileSize'.")] string operationName,
        [Description("A brief description of what you were trying to accomplish.")] string description)
    {
        throw new NotSupportedException($"File system operation '{operationName}' is not supported: {description}");
    }
}
