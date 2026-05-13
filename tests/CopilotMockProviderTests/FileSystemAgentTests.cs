using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace CopilotMockProvider;

public class FileSystemAgentTests(CopilotTestFixture copilot) : IClassFixture<CopilotTestFixture>
{
    [Fact]
    public async Task Agent_ReadsFile_AndUpdatesIt()
    {
        var mock = copilot.CreateAgentMock(builder =>
        {
            builder.AddFileSystem(
                new ManifestEmbeddedFileProvider(typeof(FileSystemAgentTests).Assembly, "testdata"));
        });

        const string Prompt = """
            Make sure to output what the full path of any file you are trying to read and write.

            Step 1: Read the file "hello.txt".

            Step 2: Create a new file called "summary.txt" whose content is the
                    the number of words in "hello.txt".

            Step 3: Read "summary.txt" back and confirm it exists.
            """;

        await mock.Agent.RunAsync(Prompt);

        var content = mock.Services.GetRequiredService<IFileProvider>()
            .GetFileContents("summary.txt");

        Assert.Equal("5", content);
    }
}
