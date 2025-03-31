using System.Text;

namespace ZipSFX;

/// <summary>Класс, представляющий запись в центральном каталоге zip-файла</summary>
internal class ZipCentralDirectoryEntry
{
    /// <summary>Имя файла</summary>
    public string FileName { get; private set; } = null!;

    /// <summary>Размер сжатого файла</summary>
    public long CompressedSize { get; private set; }

    /// <summary>Размер несжатого файла</summary>
    public long UncompressedSize { get; private set; }

    /// <summary>Смещение локального заголовка файла</summary>
    public long LocalHeaderOffset { get; private set; }

    /// <summary>Читает запись центрального каталога из потока</summary>
    /// <param name="stream">Поток данных zip-файла</param>
    public void Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        // Пример чтения записи центрального каталога
        LocalHeaderOffset = reader.ReadUInt32();
        CompressedSize = reader.ReadUInt32();
        UncompressedSize = reader.ReadUInt32();
        var file_name_length = reader.ReadUInt16();
        FileName = Encoding.UTF8.GetString(reader.ReadBytes(file_name_length));
    }
}