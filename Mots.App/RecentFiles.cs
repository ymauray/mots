using System.IO;
using System.Text.Json;

namespace Mots;

/// <summary>
/// Liste des derniers documents ouverts ou enregistrés, du plus récent au plus ancien, conservée entre les sessions
/// dans un petit fichier JSON. Toute erreur d'accès au fichier est ignorée : la liste est un confort, pas une donnée.
/// </summary>
public sealed class RecentFiles
{
    public const int DefaultCapacity = 10;

    private readonly string? _storagePath;
    private readonly int _capacity;
    private readonly List<string> _items = [];

    /// <param name="storagePath">Fichier de persistance ; null pour une liste purement en mémoire.</param>
    public RecentFiles(string? storagePath = null, int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _storagePath = storagePath;
        _capacity = capacity;
        Load();
    }

    public static RecentFiles CreateDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mots", "recent.json"));

    public IReadOnlyList<string> Items => _items;

    /// <summary>Place le fichier en tête (sans doublon, sans tenir compte de la casse) et borne la liste.</summary>
    public void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        path = Path.GetFullPath(path);

        _items.RemoveAll(p => Same(p, path));
        _items.Insert(0, path);
        if (_items.Count > _capacity)
            _items.RemoveRange(_capacity, _items.Count - _capacity);
        Save();
    }

    public void Remove(string path)
    {
        if (_items.RemoveAll(p => Same(p, path)) > 0)
            Save();
    }

    public void Clear()
    {
        if (_items.Count == 0) return;
        _items.Clear();
        Save();
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private void Load()
    {
        if (_storagePath is null) return;
        try
        {
            var stored = JsonSerializer.Deserialize<string[]>(File.ReadAllText(_storagePath));
            foreach (var path in stored ?? [])
            {
                if (!string.IsNullOrWhiteSpace(path) && !_items.Exists(p => Same(p, path)))
                    _items.Add(path);
            }
            if (_items.Count > _capacity)
                _items.RemoveRange(_capacity, _items.Count - _capacity);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            _items.Clear(); // fichier absent, illisible ou d'un autre format : on repart d'une liste vide
        }
    }

    private void Save()
    {
        if (_storagePath is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)!);
            File.WriteAllText(_storagePath, JsonSerializer.Serialize(_items));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // pas de persistance possible : la liste reste valable pour la session
        }
    }
}
