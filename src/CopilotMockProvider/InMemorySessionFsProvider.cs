using System.Collections;
using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace CopilotMockProvider;

public class InMemorySessionFsProvider : IFileProvider
{
    private readonly IFileProvider? _provider;
    private readonly Dictionary<string, InMemoryFile> _files = new();
    private readonly Dictionary<string, DateTimeOffset> _directories = new();

    public InMemorySessionFsProvider(IFileProvider? provider = null)
    {
        _provider = provider;
        _directories["/"] = DateTimeOffset.UtcNow;
    }

    // Public structured API

    public IReadOnlyList<FileSystemEntry>? ListFiles(string path)
    {
        var normalized = NormalizePath(path);

        if (!_directories.ContainsKey(normalized))
        {
            if (_provider != null)
            {
                var contents = _provider.GetDirectoryContents(ToProviderSubpath(normalized));
                if (!contents.Exists)
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        var children = GetDirectChildren(normalized);
        var result = new List<FileSystemEntry>(children.Count);
        foreach (var (name, isFile) in children.OrderBy(e => e.Key))
        {
            result.Add(new FileSystemEntry(name, !isFile));
        }

        return result;
    }

    public string? ReadFile(string path)
    {
        var normalized = NormalizePath(path);

        if (_files.TryGetValue(normalized, out var file))
        {
            return file.Content;
        }

        if (_provider != null && ExistsInProvider(normalized))
        {
            return ReadFromProvider(normalized);
        }

        return null;
    }

    public void WriteFile(string path, string content)
    {
        var normalized = NormalizePath(path);

        EnsureParentDirectories(normalized);

        var now = DateTimeOffset.UtcNow;
        if (_files.TryGetValue(normalized, out var existingFile))
        {
            existingFile.Content = content;
            existingFile.LastModified = now;
        }
        else
        {
            _files[normalized] = new InMemoryFile
            {
                Name = GetFileName(normalized),
                Content = content,
                LastModified = now,
                CreatedAt = now
            };
        }
    }

    public bool Delete(string path, bool recursive = false)
    {
        var normalized = NormalizePath(path);

        var existsAsFile = _files.ContainsKey(normalized);
        var existsAsDir = _directories.ContainsKey(normalized);

        if (!existsAsFile && !existsAsDir)
        {
            if (_provider != null && ExistsInProvider(normalized))
            {
                throw new InvalidOperationException($"Cannot delete provider-only path: {path}");
            }

            return false;
        }

        if (existsAsFile)
        {
            _files.Remove(normalized);
        }
        else if (existsAsDir)
        {
            var prefix = GetChildPrefix(normalized);

            if (recursive)
            {
                var filesToRemove = _files.Keys.Where(f => f.StartsWith(prefix) || f == normalized).ToList();
                foreach (var file in filesToRemove)
                {
                    _files.Remove(file);
                }

                var dirsToRemove = _directories.Keys.Where(d => d.StartsWith(prefix) || d == normalized).ToList();
                foreach (var dir in dirsToRemove)
                {
                    _directories.Remove(dir);
                }
            }
            else
            {
                var hasChildren = _files.Keys.Any(f => f.StartsWith(prefix)) ||
                                _directories.Keys.Any(d => d != normalized && d.StartsWith(prefix));

                if (hasChildren)
                {
                    throw new InvalidOperationException($"Directory not empty: {path}");
                }

                _directories.Remove(normalized);
            }
        }

        return true;
    }

    public bool Rename(string sourcePath, string destinationPath)
    {
        var normalizedSrc = NormalizePath(sourcePath);
        var normalizedDest = NormalizePath(destinationPath);

        if (_files.ContainsKey(normalizedSrc) || _directories.ContainsKey(normalizedSrc))
        {
            RenameCore(normalizedSrc, normalizedDest, sourcePath, destinationPath);
            return true;
        }

        if (_provider != null && ExistsInProvider(normalizedSrc))
        {
            var content = ReadFromProvider(normalizedSrc);
            if (!_files.ContainsKey(normalizedSrc))
            {
                var now = DateTimeOffset.UtcNow;
                _files[normalizedSrc] = new InMemoryFile
                {
                    Name = GetFileName(normalizedSrc),
                    Content = content,
                    LastModified = now,
                    CreatedAt = now
                };
            }
            RenameCore(normalizedSrc, normalizedDest, sourcePath, destinationPath);
            return true;
        }

        return false;
    }

    public void CreateDirectory(string path, bool recursive = true)
    {
        var normalized = NormalizePath(path);

        if (recursive)
        {
            EnsureParentDirectories(normalized);
        }

        _directories.TryAdd(normalized, DateTimeOffset.UtcNow);
    }

    public bool Exists(string path)
    {
        var normalized = NormalizePath(path);

        if (_files.ContainsKey(normalized) || _directories.ContainsKey(normalized))
        {
            return true;
        }

        return _provider != null && ExistsInProvider(normalized);
    }

    // IFileProvider

    IFileInfo IFileProvider.GetFileInfo(string subpath)
    {
        var normalized = NormalizePath(subpath);

        if (_files.TryGetValue(normalized, out var file))
        {
            return file;
        }

        if (_provider != null)
        {
            var info = _provider.GetFileInfo(ToProviderSubpath(normalized));
            if (info.Exists)
            {
                return info;
            }
        }

        return new NotFoundFileInfo(GetFileName(normalized));
    }

    IDirectoryContents IFileProvider.GetDirectoryContents(string subpath)
    {
        var normalized = NormalizePath(subpath);

        if (!_directories.ContainsKey(normalized))
        {
            if (_provider != null)
            {
                var providerContents = _provider.GetDirectoryContents(ToProviderSubpath(normalized));
                if (providerContents.Exists)
                {
                    var children = GetDirectChildren(normalized);
                    return BuildDirectoryContents(normalized, children, exists: true);
                }
            }

            return NotFoundDirectoryContents.Singleton;
        }

        var entries = GetDirectChildren(normalized);
        return BuildDirectoryContents(normalized, entries, exists: true);
    }

    IChangeToken IFileProvider.Watch(string filter) => NullChangeToken.Singleton;

    // Internal AI tool methods

    [Description("List the files and directories at the given path.")]
    internal string ListFilesTool(
        [Description("The relative path of the directory to list, e.g. '' (root) or 'src'.")] string path)
    {
        var entries = ListFiles(path);

        if (entries is null)
        {
            return $"Directory not found: {path}";
        }

        if (entries.Count == 0)
        {
            return $"Directory '{path}' is empty.";
        }

        var sb = new StringBuilder();
        foreach (var entry in entries)
        {
            sb.AppendLine(entry.IsDirectory ? $"[dir]  {entry.Name}" : $"[file] {entry.Name}");
        }

        return sb.ToString();
    }

    [Description("Read the text content of a file.")]
    internal string ReadFileTool(
        [Description("The relative path of the file to read, e.g. 'hello.txt'.")] string path)
    {
        return ReadFile(path) ?? $"File not found: {path}";
    }

    [Description("Write or overwrite a file with the given content. Parent directories are created automatically.")]
    internal string WriteFileTool(
        [Description("The relative path of the file to write, e.g. 'src/main.cs'.")] string path,
        [Description("The text content to write to the file.")] string content)
    {
        WriteFile(path, content);
        return $"File written: {path}";
    }

    [Description("Delete a file or directory.")]
    internal string DeleteTool(
        [Description("The relative path of the file or directory to delete.")] string path,
        [Description("If true, delete directories and their contents recursively.")] bool recursive = false)
    {
        try
        {
            if (Delete(path, recursive))
            {
                return $"Deleted: {path}";
            }

            return $"Path not found: {path}";
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    [Description("Rename or move a file or directory to a new path.")]
    internal string RenameTool(
        [Description("The current path of the file or directory.")] string sourcePath,
        [Description("The new path for the file or directory.")] string destinationPath)
    {
        try
        {
            if (Rename(sourcePath, destinationPath))
            {
                return $"Renamed '{sourcePath}' to '{destinationPath}'.";
            }

            return $"Source path not found: {sourcePath}";
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    // Private static path helpers

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "/";
        }

        var normalized = path.Replace('\\', '/');

        while (normalized.Contains("//"))
        {
            normalized = normalized.Replace("//", "/");
        }

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        if (normalized.Length > 1 && normalized.EndsWith('/'))
        {
            normalized = normalized.TrimEnd('/');
        }

        return normalized;
    }

    private static string? GetParentDirectory(string normalizedPath)
    {
        if (normalizedPath == "/")
        {
            return null;
        }

        var lastSlash = normalizedPath.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : normalizedPath[..lastSlash];
    }

    private static string GetChildPrefix(string normalizedPath)
        => normalizedPath == "/" ? "/" : normalizedPath + "/";

    private static string GetFileName(string normalizedPath)
    {
        var lastSlash = normalizedPath.LastIndexOf('/');
        return lastSlash >= 0 ? normalizedPath[(lastSlash + 1)..] : normalizedPath;
    }

    /// <summary>
    /// Converts a normalized path (e.g. "/foo/bar.txt") to a provider-relative subpath ("foo/bar.txt").
    /// </summary>
    private static string ToProviderSubpath(string normalizedPath)
        => normalizedPath == "/" ? "" : normalizedPath[1..];

    // Private instance helpers

    private bool ExistsInProvider(string normalizedPath)
    {
        if (_provider == null)
        {
            return false;
        }

        var fileInfo = _provider.GetFileInfo(ToProviderSubpath(normalizedPath));
        return fileInfo.Exists;
    }

    private string ReadFromProvider(string normalizedPath)
    {
        if (_provider == null)
        {
            throw new FileNotFoundException($"File not found: {normalizedPath}");
        }

        var fileInfo = _provider.GetFileInfo(ToProviderSubpath(normalizedPath));

        if (!fileInfo.Exists || fileInfo.IsDirectory)
        {
            throw new FileNotFoundException($"File not found: {normalizedPath}");
        }

        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private void EnsureParentDirectories(string normalizedPath)
    {
        var parent = GetParentDirectory(normalizedPath);
        while (parent != null && !_directories.ContainsKey(parent))
        {
            _directories.TryAdd(parent, DateTimeOffset.UtcNow);
            parent = GetParentDirectory(parent);
        }
    }

    private Dictionary<string, bool> GetDirectChildren(string normalizedPath)
    {
        var entries = new Dictionary<string, bool>(); // name -> isFile
        var prefix = GetChildPrefix(normalizedPath);

        foreach (var filePath in _files.Keys)
        {
            if (filePath.StartsWith(prefix))
            {
                var remainder = filePath[prefix.Length..];
                var slashIndex = remainder.IndexOf('/');
                if (slashIndex < 0)
                {
                    entries[remainder] = true;
                }
                else
                {
                    var dirName = remainder[..slashIndex];
                    entries.TryAdd(dirName, false);
                }
            }
        }

        foreach (var dirPath in _directories.Keys)
        {
            if (dirPath != normalizedPath && dirPath.StartsWith(prefix))
            {
                var remainder = dirPath[prefix.Length..];
                var slashIndex = remainder.IndexOf('/');
                var entryName = slashIndex >= 0 ? remainder[..slashIndex] : remainder;
                if (entryName.Length > 0)
                {
                    entries.TryAdd(entryName, false);
                }
            }
        }

        if (_provider != null)
        {
            var contents = _provider.GetDirectoryContents(ToProviderSubpath(normalizedPath));
            if (contents.Exists)
            {
                foreach (var entry in contents)
                {
                    entries.TryAdd(entry.Name, !entry.IsDirectory);
                }
            }
        }

        return entries;
    }

    private void RenameCore(string normalizedSrc, string normalizedDest, string sourcePath, string destinationPath)
    {
        var srcIsFile = _files.ContainsKey(normalizedSrc);

        if (_files.ContainsKey(normalizedDest) || _directories.ContainsKey(normalizedDest))
        {
            throw new InvalidOperationException($"Destination path already exists: {destinationPath}");
        }

        var destParent = GetParentDirectory(normalizedDest);
        if (destParent != null && !_directories.ContainsKey(destParent))
        {
            throw new InvalidOperationException($"Destination parent directory not found: {destParent}");
        }

        if (srcIsFile)
        {
            var file = _files[normalizedSrc];
            _files.Remove(normalizedSrc);
            _files[normalizedDest] = file;
            file.Name = GetFileName(normalizedDest);
            file.LastModified = DateTimeOffset.UtcNow;
        }
        else
        {
            var srcPrefix = GetChildPrefix(normalizedSrc);
            var destPrefix = GetChildPrefix(normalizedDest);

            var filesToRename = _files.Where(kvp => kvp.Key == normalizedSrc || kvp.Key.StartsWith(srcPrefix)).ToList();
            foreach (var kvp in filesToRename)
            {
                var newPath = kvp.Key == normalizedSrc
                    ? normalizedDest
                    : destPrefix + kvp.Key[srcPrefix.Length..];
                _files.Remove(kvp.Key);
                _files[newPath] = kvp.Value;
                kvp.Value.Name = GetFileName(newPath);
                kvp.Value.LastModified = DateTimeOffset.UtcNow;
            }

            var dirsToRename = _directories.Keys.Where(d => d == normalizedSrc || d.StartsWith(srcPrefix)).ToList();
            foreach (var dir in dirsToRename)
            {
                var newPath = dir == normalizedSrc
                    ? normalizedDest
                    : destPrefix + dir[srcPrefix.Length..];
                var timestamp = _directories[dir];
                _directories.Remove(dir);
                _directories[newPath] = timestamp;
            }
        }
    }

    private InMemoryDirectoryContents BuildDirectoryContents(string normalizedParent, Dictionary<string, bool> children, bool exists)
    {
        var prefix = GetChildPrefix(normalizedParent);
        var entries = new List<IFileInfo>(children.Count);

        foreach (var (name, isFile) in children)
        {
            if (isFile)
            {
                var childPath = prefix + name;
                if (_files.TryGetValue(childPath, out var file))
                {
                    entries.Add(file);
                }
                else if (_provider != null)
                {
                    var info = _provider.GetFileInfo(ToProviderSubpath(childPath));
                    if (info.Exists)
                    {
                        entries.Add(info);
                    }
                }
            }
            else
            {
                entries.Add(new InMemoryDirectoryInfo(name));
            }
        }

        return new InMemoryDirectoryContents(entries, exists);
    }

    // Nested types

    public record FileSystemEntry(string Name, bool IsDirectory);

    private sealed class InMemoryFile : IFileInfo
    {
        public required string Name { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTimeOffset LastModified { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        bool IFileInfo.Exists => true;
        bool IFileInfo.IsDirectory => false;
        long IFileInfo.Length => Encoding.UTF8.GetByteCount(Content);
        DateTimeOffset IFileInfo.LastModified => LastModified;
        string? IFileInfo.PhysicalPath => null;
        Stream IFileInfo.CreateReadStream() => new MemoryStream(Encoding.UTF8.GetBytes(Content));
    }

    private sealed class InMemoryDirectoryInfo(string name) : IFileInfo
    {
        public bool Exists => true;
        public bool IsDirectory => true;
        public long Length => -1;
        public DateTimeOffset LastModified => DateTimeOffset.MinValue;
        public string Name => name;
        public string? PhysicalPath => null;
        public Stream CreateReadStream() => throw new InvalidOperationException("Cannot read a directory.");
    }

    private sealed class InMemoryDirectoryContents(List<IFileInfo> entries, bool exists) : IDirectoryContents
    {
        public bool Exists => exists;
        public IEnumerator<IFileInfo> GetEnumerator() => entries.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
