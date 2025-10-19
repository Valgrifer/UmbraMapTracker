using System.IO;

namespace UmbraMapTracker.Services;

public partial class Config
{
    private static DirectoryInfo ConfigDirectory => new(Framework.DalamudPlugin.ConfigDirectory.FullName);

    /// <summary>
    /// Returns true if a file with the given name exists in the plugin's
    /// config directory.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    private static bool FileExists(string fileName)
    {
        return new FileInfo(Path.Combine(ConfigDirectory.FullName, fileName)).Exists;
    }

    /// <summary>
    /// Reads a JSON file from the config directory.
    /// </summary>
    /// <param name="fileName">The name of the JSON file.</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    private static T? ReadFile<T>(string fileName)
    {
        FileInfo file = new(Path.Combine(ConfigDirectory.FullName, fileName));

        return file.Exists
            ? JsonConvert.DeserializeObject<T>(File.ReadAllText(file.FullName))
            : default;
    }

    /// <summary>
    /// Writes a JSON file to the config directory.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    private static void WriteFile<T>(string fileName, T obj)
    {
        FileInfo file = new(Path.Combine(ConfigDirectory.FullName, fileName));
        File.WriteAllText(file.FullName, JsonConvert.SerializeObject(obj, Formatting.Indented));
    }
}