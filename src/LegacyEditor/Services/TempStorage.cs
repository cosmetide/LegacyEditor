using System.IO;

namespace LegacyEditor.Services;

public static class TempStorage
{
    static string? _basePath;

    public static string BasePath
    {
        get
        {
            if (_basePath != null) return _basePath;

            var exePath = System.Reflection.Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrEmpty(exePath))
            {
                var dir = Path.GetDirectoryName(exePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    _basePath = Path.Combine(dir, "TempStorage");
                }
            }

            if (string.IsNullOrEmpty(_basePath))
            {
                _basePath = Path.Combine(AppContext.BaseDirectory, "TempStorage");
            }

            Directory.CreateDirectory(_basePath);
            return _basePath;
        }
        set => _basePath = value;
    }

    public static string GetTempFile(string prefix = "temp", string extension = ".tmp")
        => Path.Combine(BasePath, $"{prefix}_{Guid.NewGuid():N}{extension}");

    public static string GetTempDirectory(string prefix = "temp")
    {
        var path = Path.Combine(BasePath, $"{prefix}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    public static void Cleanup()
    {
        if (Directory.Exists(BasePath))
        {
            try { Directory.Delete(BasePath, true); } catch { }
        }
    }
}