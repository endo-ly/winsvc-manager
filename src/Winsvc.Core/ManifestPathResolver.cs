using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Winsvc.Core;

public static class ManifestPathResolver
{
    public const string ManifestDirectoryEnvironmentVariable = "WINSVC_MANIFEST_DIR";

    public static string ResolveDirectory(string? configuredPath, params string[] baseDirectories)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            candidates.Add(configuredPath);
        }

        var environmentPath = Environment.GetEnvironmentVariable(ManifestDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            candidates.Add(environmentPath);
        }

        foreach (var baseDirectory in baseDirectories.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            candidates.Add(Path.Combine(baseDirectory, "manifests"));
        }

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return Path.GetFullPath(candidates.FirstOrDefault() ?? "manifests");
    }

    public static IReadOnlyList<string> EnumerateManifestPaths(string manifestDirectory)
    {
        if (!Directory.Exists(manifestDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory
            .EnumerateFiles(manifestDirectory, "*.y*ml", SearchOption.TopDirectoryOnly)
            .Where(path => !IsTemplatePath(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string? FindManifestPath(string manifestDirectory, string id)
    {
        var candidates = new[]
        {
            Path.Combine(manifestDirectory, $"{id}.yaml"),
            Path.Combine(manifestDirectory, $"{id}.yml")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static bool IsTemplatePath(string path)
    {
        return path.EndsWith(".template.yaml", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".template.yml", StringComparison.OrdinalIgnoreCase);
    }
}
