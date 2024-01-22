using System.Reflection;
using System.Text;

namespace Unosquare.Ser2Net.Resources;

internal static class ResourceManager
{
    private static readonly Lazy<string> installScriptWindows = new(() => ReadString("install-win.bat"), isThreadSafe: false);

    private static string ReadString(string resourceName)
    {
        var matchingResources = ResourceNames
            .Where(c => c.Contains(resourceName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matchingResources.Length <= 0)
            throw new KeyNotFoundException($"Could not find resource matching '{resourceName}'.");

        if (matchingResources.Length > 1)
            throw new KeyNotFoundException($"Multiple resources found while matching '{resourceName}'.");

        resourceName = matchingResources[0];
        using var readStream = Assembly.GetManifestResourceStream(resourceName) ??
            throw new IOException($"Could not read resource stream for '{resourceName}'");
        using var reader = new StreamReader(readStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static Assembly Assembly { get; } = typeof(ResourceManager).Assembly;

    public static IReadOnlyList<string> ResourceNames { get; } =
        Assembly.GetManifestResourceNames();

    public static string InstallScriptWindows => installScriptWindows.Value;
}
