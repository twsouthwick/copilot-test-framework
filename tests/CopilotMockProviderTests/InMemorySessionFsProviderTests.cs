using Microsoft.Extensions.FileProviders;

namespace CopilotMockProvider;

public class InMemorySessionFsProviderTests
{
    private static ManifestEmbeddedFileProvider CreateEmbeddedProvider()
        => new(typeof(InMemorySessionFsProviderTests).Assembly, "testdata");

    private static async Task<string> ReadFileInfoContentAsync(IFileInfo fileInfo)
    {
        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task WriteFile_ThenGetFileInfo_ReturnsContent()
    {
        var files = new InMemorySessionFsProvider();
        var readable = (IFileProvider)files;

        files.WriteFile("test.txt", "hello world");

        var info = readable.GetFileInfo("test.txt");
        Assert.True(info.Exists);
        Assert.False(info.IsDirectory);
        Assert.Equal("hello world", await ReadFileInfoContentAsync(info));
    }

    [Fact]
    public async Task WriteFile_Overwrite_UpdatesContent()
    {
        var files = new InMemorySessionFsProvider();
        var readable = (IFileProvider)files;

        files.WriteFile("doc.txt", "v1");
        files.WriteFile("doc.txt", "v2");

        var info = readable.GetFileInfo("doc.txt");
        Assert.Equal("v2", await ReadFileInfoContentAsync(info));
    }

    [Fact]
    public void CreateDirectory_ThenGetDirectoryContents_Exists()
    {
        var files = new InMemorySessionFsProvider();
        var readable = (IFileProvider)files;

        files.CreateDirectory("mydir");

        var contents = readable.GetDirectoryContents("mydir");
        Assert.True(contents.Exists);
    }

    [Fact]
    public void WriteFiles_GetDirectoryContents_ListsChildren()
    {
        var files = new InMemorySessionFsProvider();
        var readable = (IFileProvider)files;

        files.CreateDirectory("src");
        files.WriteFile("src/a.txt", "a");
        files.WriteFile("src/b.txt", "b");

        var contents = readable.GetDirectoryContents("src");
        Assert.True(contents.Exists);

        var names = contents.Select(e => e.Name).OrderBy(n => n).ToList();
        Assert.Equal(["a.txt", "b.txt"], names);
    }

    [Fact]
    public void WriteFile_ToExistingDirectory_MergesIntoListing()
    {
        var files = new InMemorySessionFsProvider();
        var readable = (IFileProvider)files;

        files.CreateDirectory("src");
        files.WriteFile("src/a.txt", "a");

        var before = readable.GetDirectoryContents("src").Select(e => e.Name).ToList();
        Assert.Single(before);
        Assert.Contains("a.txt", before);

        files.WriteFile("src/b.txt", "b");

        var after = readable.GetDirectoryContents("src").Select(e => e.Name).OrderBy(n => n).ToList();
        Assert.Equal(["a.txt", "b.txt"], after);
    }

    [Fact]
    public void NestedFiles_GetDirectoryContents_ListsImmediateChildrenOnly()
    {
        var files = new InMemorySessionFsProvider();
        var readable = (IFileProvider)files;

        files.CreateDirectory("root/child");
        files.WriteFile("root/top.txt", "top");
        files.WriteFile("root/child/deep.txt", "deep");

        var contents = readable.GetDirectoryContents("root");
        var entries = contents.ToList();

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Name == "top.txt" && !e.IsDirectory);
        Assert.Contains(entries, e => e.Name == "child" && e.IsDirectory);
    }

    [Fact]
    public void DeleteFile_GetFileInfo_NotFound()
    {
        var files = new InMemorySessionFsProvider();
        var readable = (IFileProvider)files;

        files.WriteFile("temp.txt", "gone soon");
        Assert.True(files.Delete("temp.txt"));

        var info = readable.GetFileInfo("temp.txt");
        Assert.False(info.Exists);
    }

    [Fact]
    public void DeleteDir_Recursive_ClearsAll()
    {
        var files = new InMemorySessionFsProvider();
        var readable = (IFileProvider)files;

        files.CreateDirectory("dir");
        files.WriteFile("dir/a.txt", "a");
        files.WriteFile("dir/b.txt", "b");

        Assert.True(files.Delete("dir", recursive: true));

        var contents = readable.GetDirectoryContents("dir");
        Assert.False(contents.Exists);
    }

    [Fact]
    public async Task RenameFile_FoundAtNewPath()
    {
        var files = new InMemorySessionFsProvider();
        var readable = (IFileProvider)files;

        files.WriteFile("old.txt", "moved");
        Assert.True(files.Rename("old.txt", "new.txt"));

        Assert.False(readable.GetFileInfo("old.txt").Exists);

        var info = readable.GetFileInfo("new.txt");
        Assert.True(info.Exists);
        Assert.Equal("moved", await ReadFileInfoContentAsync(info));
    }

    [Fact]
    public void BackingProvider_GetFileInfo_ReturnsEmbeddedFile()
    {
        var embedded = CreateEmbeddedProvider();
        var files = new InMemorySessionFsProvider(embedded);
        var readable = (IFileProvider)files;

        var info = readable.GetFileInfo("hello.txt");
        Assert.True(info.Exists);
        Assert.False(info.IsDirectory);
    }

    [Fact]
    public async Task BackingProvider_GetFileInfo_ReturnsEmbeddedContent()
    {
        var embedded = CreateEmbeddedProvider();
        var files = new InMemorySessionFsProvider(embedded);
        var readable = (IFileProvider)files;

        var info = readable.GetFileInfo("hello.txt");
        Assert.Equal("Hello from embedded test data!", await ReadFileInfoContentAsync(info));
    }

    [Fact]
    public void BackingProvider_GetDirectoryContents_InMemoryEntriesListed()
    {
        var embedded = CreateEmbeddedProvider();
        var files = new InMemorySessionFsProvider(embedded);
        var readable = (IFileProvider)files;

        files.WriteFile("inmemory.txt", "dynamic");

        var contents = readable.GetDirectoryContents("");
        Assert.True(contents.Exists);

        var names = contents.Select(e => e.Name).ToList();
        Assert.Contains("inmemory.txt", names);

        var embeddedFile = readable.GetFileInfo("hello.txt");
        Assert.True(embeddedFile.Exists);
    }

    [Fact]
    public async Task BackingProvider_WriteOverrides_ProviderFile()
    {
        var embedded = CreateEmbeddedProvider();
        var files = new InMemorySessionFsProvider(embedded);
        var readable = (IFileProvider)files;

        files.WriteFile("hello.txt", "overridden!");

        var info = readable.GetFileInfo("hello.txt");
        Assert.True(info.Exists);
        Assert.Equal("overridden!", await ReadFileInfoContentAsync(info));
    }

    [Fact]
    public void BackingProvider_NestedFile_Accessible()
    {
        var embedded = CreateEmbeddedProvider();
        var files = new InMemorySessionFsProvider(embedded);
        var readable = (IFileProvider)files;

        var info = readable.GetFileInfo("subdir/nested.txt");
        Assert.True(info.Exists);
    }

    [Fact]
    public void GetFileInfo_NonExistent_NotFound()
    {
        var files = new InMemorySessionFsProvider();
        var readable = (IFileProvider)files;

        var info = readable.GetFileInfo("nope.txt");
        Assert.False(info.Exists);
    }

    [Fact]
    public void GetDirectoryContents_NonExistent_NotFound()
    {
        var files = new InMemorySessionFsProvider();
        var readable = (IFileProvider)files;

        var contents = readable.GetDirectoryContents("missing");
        Assert.False(contents.Exists);
    }

    [Fact]
    public void Watch_ReturnsNullChangeToken()
    {
        var files = new InMemorySessionFsProvider();
        var readable = (IFileProvider)files;

        var token = readable.Watch("**/*");
        Assert.False(token.HasChanged);
        Assert.False(token.ActiveChangeCallbacks);
    }

    [Fact]
    public void ListFiles_ReturnsEntries()
    {
        var files = new InMemorySessionFsProvider();

        files.CreateDirectory("src");
        files.WriteFile("src/main.cs", "code");
        files.WriteFile("src/util.cs", "more code");

        var listing = files.ListFiles("src");
        Assert.NotNull(listing);
        Assert.Equal(2, listing.Count);
        Assert.Contains(listing, e => e.Name == "main.cs" && !e.IsDirectory);
        Assert.Contains(listing, e => e.Name == "util.cs" && !e.IsDirectory);
    }

    [Fact]
    public void ListFiles_NonExistentDirectory_ReturnsNull()
    {
        var files = new InMemorySessionFsProvider();

        Assert.Null(files.ListFiles("nope"));
    }

    [Fact]
    public void ReadFile_ReturnsContent()
    {
        var files = new InMemorySessionFsProvider();

        files.WriteFile("test.txt", "hello");

        Assert.Equal("hello", files.ReadFile("test.txt"));
    }

    [Fact]
    public void ReadFile_NonExistent_ReturnsNull()
    {
        var files = new InMemorySessionFsProvider();

        Assert.Null(files.ReadFile("nope.txt"));
    }

    [Fact]
    public void ReadFile_FromProvider_ReturnsContent()
    {
        var embedded = CreateEmbeddedProvider();
        var files = new InMemorySessionFsProvider(embedded);

        Assert.Equal("Hello from embedded test data!", files.ReadFile("hello.txt"));
    }

    [Fact]
    public void Exists_ReturnsTrueForFile()
    {
        var files = new InMemorySessionFsProvider();

        files.WriteFile("test.txt", "content");

        Assert.True(files.Exists("test.txt"));
        Assert.False(files.Exists("nope.txt"));
    }
}
