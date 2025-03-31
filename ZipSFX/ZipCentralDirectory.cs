namespace ZipSFX;

/// <summary>Класс, представляющий центральный каталог zip-файла</summary>
internal class ZipCentralDirectory
{
    private ZipCentralDirectoryEntry[] _Entries = [];

    /// <summary>Список записей центрального каталога</summary>
    public IReadOnlyList<ZipCentralDirectoryEntry> Entries => _Entries;

    /// <summary>Читает записи центрального каталога из потока</summary>
    /// <param name="stream">Поток данных zip-файла</param>
    public void Read(Stream stream)
    {
        var entries = new List<ZipCentralDirectoryEntry>();

        // Пример чтения записей центрального каталога
        while (stream.Position < stream.Length)
        {
            var entry = new ZipCentralDirectoryEntry();
            entry.Read(stream);
            entries.Add(entry);
        }

        _Entries = [.. entries];
    }
}