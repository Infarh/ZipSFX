using System.Text;

namespace ZipSFX;

/// <summary>Класс, представляющий запись в центральном каталоге zip-файла</summary>
internal class ZipCentralDirectoryEntry
{
    /// <summary>Имя файла</summary>
    public string FileName { get; private set; } = null!;

    /// <summary>CRC-32 несжатых данных</summary>
    public uint Crc32 { get; private set; }

    /// <summary>Размер сжатого файла</summary>
    public uint CompressedSize { get; private set; }

    /// <summary>Размер несжатого файла</summary>
    public uint UncompressedSize { get; private set; }

    /// <summary>Смещение локального заголовка файла (от начала архива)</summary>
    public uint LocalHeaderOffset { get; private set; }

    /// <summary>Метод сжатия</summary>
    public ushort CompressionMethod { get; private set; }

    /// <summary>Флаги общего назначения</summary>
    public ushort GeneralPurposeBitFlag { get; private set; }

    /// <summary>Читает запись центрального каталога из потока</summary>
    /// <param name="stream">Поток данных zip-файла</param>
    public void Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        // central directory file header signature (0x02014b50)
        var signature = reader.ReadUInt32();
        if (signature != 0x02014b50)
            throw new InvalidDataException("Неверная сигнатура записи центрального каталога");

        _ = reader.ReadUInt16();                 // version made by
        _ = reader.ReadUInt16();                 // version needed to extract
        GeneralPurposeBitFlag = reader.ReadUInt16();
        CompressionMethod = reader.ReadUInt16();
        _ = reader.ReadUInt16();                 // last mod file time
        _ = reader.ReadUInt16();                 // last mod file date
        Crc32 = reader.ReadUInt32();
        CompressedSize = reader.ReadUInt32();
        UncompressedSize = reader.ReadUInt32();
        var file_name_length = reader.ReadUInt16();
        var extra_field_length = reader.ReadUInt16();
        var file_comment_length = reader.ReadUInt16();
        _ = reader.ReadUInt16();                 // disk number start
        _ = reader.ReadUInt16();                 // internal file attributes
        _ = reader.ReadUInt32();                 // external file attributes
        LocalHeaderOffset = reader.ReadUInt32();

        FileName = Encoding.UTF8.GetString(reader.ReadBytes(file_name_length));
        if (extra_field_length > 0)
            reader.ReadBytes(extra_field_length);
        if (file_comment_length > 0)
            reader.ReadBytes(file_comment_length);
    }
}