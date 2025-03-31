using System.Text;

namespace ZipSFX;

/// <summary>Класс, представляющий локальный заголовок файла в zip-файле</summary>
internal class ZipLocalFileHeader
{
    /// <summary>Имя файла</summary>
    public string FileName { get; private set; } = null!;

    /// <summary>Размер сжатого файла</summary>
    public long CompressedSize { get; private set; }

    /// <summary>Размер несжатого файла</summary>
    public long UncompressedSize { get; private set; }

    /// <summary>Данные файла</summary>
    public byte[] FileData { get; private set; } = null!;

    /// <summary>Читает локальный заголовок файла из потока</summary>
    /// <param name="stream">Поток данных zip-файла</param>
    public void Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        // Пример чтения локального заголовка файла
        reader.ReadUInt32(); // Signature
        reader.ReadUInt16(); // Version needed to extract
        reader.ReadUInt16(); // General purpose bit flag
        reader.ReadUInt16(); // Compression method
        reader.ReadUInt16(); // Last mod file time
        reader.ReadUInt16(); // Last mod file date
        reader.ReadUInt32(); // CRC-32

        CompressedSize = reader.ReadUInt32();
        UncompressedSize = reader.ReadUInt32();

        var file_name_length = reader.ReadUInt16();
        var extra_field_length = reader.ReadUInt16();

        FileName = Encoding.UTF8.GetString(reader.ReadBytes(file_name_length));

        reader.ReadBytes(extra_field_length); // Extra field
        FileData = reader.ReadBytes((int)CompressedSize);
    }
}