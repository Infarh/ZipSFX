namespace ZipSFX;

/// <summary>Класс, представляющий центральный каталог zip-файла</summary>
internal class ZipCentralDirectory
{
    private ZipCentralDirectoryEntry[] _Entries = [];

    /// <summary>Список записей центрального каталога</summary>
    public IReadOnlyList<ZipCentralDirectoryEntry> Entries => _Entries;

    /// <summary>Читает записи центрального каталога из потока начиная с позиции EOCD.CentralDirectoryOffset</summary>
    /// <param name="stream">Поток данных zip-файла</param>
    public void Read(Stream stream)
    {
        var entries = new List<ZipCentralDirectoryEntry>();

        // Чтение подряд записей до конца каталога невозможно без длины каталога,
        // поэтому метод предполагает, что позиция в потоке установлена на начало каталога,
        // а конец известен вызывающей стороне. Здесь читаем до первой ошибки сигнатуры.
        var start_position = stream.Position;
        while (stream.Position < stream.Length)
        {
            var prev_pos = stream.Position;
            try
            {
                var entry = new ZipCentralDirectoryEntry();
                entry.Read(stream);
                entries.Add(entry);
            }
            catch
            {
                // Возвращаемся на предыдущую позицию и прекращаем чтение
                stream.Position = prev_pos;
                break;
            }
        }

        _Entries = [.. entries];
    }
}