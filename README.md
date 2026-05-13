# CopilotMockProvider

A .NET 10 testing library that provides a simple API for building a Copilot mock. Augments the Copilot harness to make things more testable with xUnit — add in-memory file systems, phase tracking, and custom tools to your agent sessions.

## Overview

CopilotMockProvider provides a simple API for building a Copilot mock that manages the lifecycle of a `CopilotClient`, letting you spin up mock agent sessions configured with custom tools, system messages, and file system providers. It's built on top of:

- [GitHub.Copilot.SDK](https://github.com/github/copilot-sdk-dotnet) — Copilot client and session management
- [Microsoft.Agents.AI](https://www.nuget.org/packages/Microsoft.Agents.AI) — AI agent framework
- [xUnit](https://xunit.net/) — Test framework integration via `IAsyncLifetime`

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0.300 or later)
- xUnit 2.9+

## Usage Examples

### Testing an Agent with File System Access

Use `AddFileSystem()` to give the agent read/write access to an in-memory file system. You can optionally seed it with an existing `IFileProvider` (such as embedded resources) so the agent starts with real files to work against.

```csharp
public class FileSystemAgentTests(CopilotTestFixture copilot) : IClassFixture<CopilotTestFixture>
{
    [Fact]
    public async Task Agent_ReadsFile_AndUpdatesIt()
    {
        var mock = copilot.CreateAgentMock(builder =>
        {
            builder.AddFileSystem(
                new ManifestEmbeddedFileProvider(
                    typeof(FileSystemAgentTests).Assembly, "testdata"));
        });

        const string Prompt = """
            Step 1: Read the file "hello.txt".
            Step 2: Create a new file called "summary.txt" with word count.
            Step 3: Read "summary.txt" back and confirm it exists.
            """;

        await mock.Agent.RunAsync(Prompt);

        var content = mock.Services.GetRequiredService<IFileProvider>()
            .GetFileContents("summary.txt");
        Assert.Equal("5", content);
    }
}
```

### Phase Transition Tracking

Use `AddPhaseTransitionCallback()` to observe when the agent completes sequential steps in a multi-step prompt. The callback receives the phase name and a summary, letting you assert that the agent executed each step in order.

```csharp
var list = new List<string>();
var mock = copilot.CreateAgentMock(builder =>
{
    builder.AddPhaseTransitionCallback((name, summary) => list.Add(name));
});

await mock.Agent.RunAsync("""
    Step 1: [work]
    Step 2: [work]
    Step 3: [work]
    Step 4: [work]
    """);

Assert.Equal(new[] { "Step 1", "Step 2", "Step 3", "Step 4" }, list);
```

## License

This project is licensed under the [MIT License](LICENSE.txt).
