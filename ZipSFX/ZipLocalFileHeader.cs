using System.Text;
using System;
using System.IO;

namespace ZipSFX;

/// <summary>Класс, представляющий локальный заголовок файла в zip-файле</summary>
internal class ZipLocalFileHeader
{
    /// <summary>Имя файла</summary>
    public string FileName { get; private set; } = null!;

    /// <summary>Флаги общего назначения</summary>
    public ushort GeneralPurposeBitFlag { get; private set; }

    /// <summary>Метод сжатия</summary>
    public ushort CompressionMethod { get; private set; }

    /// <summary>Размер сжатого файла</summary>
    public uint CompressedSize { get; private set; }

    /// <summary>Размер несжатого файла</summary>
    public uint UncompressedSize { get; private set; }

    /// <summary>Длина дополнительного поля</summary>
    public ushort ExtraFieldLength { get; private set; }

    /// <summary>Смещение начала данных файла относительно начала локального заголовка</summary>
    public long DataStartRelativeOffset { get; private set; }

    /// <summary>Читает локальный заголовок файла из потока</summary>
    /// <param name="stream">Поток данных zip-файла</param>
    public void Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        // local file header signature (0x04034b50)
        var signature = reader.ReadUInt32();
        if (signature != 0x04034b50)
            throw new InvalidDataException("Неверная сигнатура локального заголовка");

        _ = reader.ReadUInt16();             // version needed to extract
        GeneralPurposeBitFlag = reader.ReadUInt16();
        CompressionMethod = reader.ReadUInt16();
        _ = reader.ReadUInt16();             // last mod file time
        _ = reader.ReadUInt16();             // last mod file date
        _ = reader.ReadUInt32();             // CRC-32 (может быть 0, если используется data descriptor)

        CompressedSize = reader.ReadUInt32();
        UncompressedSize = reader.ReadUInt32();

        var file_name_length = reader.ReadUInt16();
        ExtraFieldLength = reader.ReadUInt16();

        // Выбор кодировки имени файла: если установлен флаг UTF-8 (bit 11), используем UTF8, иначе CP437
        var nameBytes = reader.ReadBytes(file_name_length);
        var nameEncoding = (GeneralPurposeBitFlag & 0x0800) != 0 ? Encoding.UTF8 : Encoding.GetEncoding(437);
        FileName = nameEncoding.GetString(nameBytes);

        // позиция начала данных файла
        DataStartRelativeOffset = 30 /*fixed header*/ + file_name_length + ExtraFieldLength; // 30 = 4+2+2+2+2+2+4+4+4+2+2

        // пропускаем extra field
        if (ExtraFieldLength > 0)
            reader.ReadBytes(ExtraFieldLength);
    }
}