using System;
using System.Collections.Generic;
using System.IO;

namespace ZipSFX;

/// <summary>Класс, представляющий центральный каталог zip-файла</summary>
internal class ZipCentralDirectory
{
    private ZipCentralDirectoryEntry[] _Entries = Array.Empty<ZipCentralDirectoryEntry>();

    /// <summary>Список записей центрального каталога</summary>
    public IReadOnlyList<ZipCentralDirectoryEntry> Entries => _Entries;

    /// <summary>Читает записи центрального каталога из потока начиная с позиции EOCD.CentralDirectoryOffset</summary>
    /// <param name="stream">Поток данных zip-файла</param>
    public void Read(Stream stream)
    {
        var entries = new List<ZipCentralDirectoryEntry>();

        // Чтение подряд записей до конца каталога — вызывающая сторона должна ограничить поток (SubReadStream).
        while (stream.Position < stream.Length)
        {
            var prev_pos = stream.Position;
            try
            {
                var entry = new ZipCentralDirectoryEntry();
                entry.Read(stream);
                entries.Add(entry);
            }
            catch (InvalidDataException)
            {
                stream.Position = prev_pos;
                break;
            }
            catch (EndOfStreamException)
            {
                stream.Position = prev_pos;
                break;
            }
        }

        _Entries = entries.ToArray();
    }
}