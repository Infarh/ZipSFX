using System.IO.Compression;
using System.Runtime.InteropServices;

using ZipSFX;

//if (args is [{ Length: > 0 } archive_file_name, ..])
//    CreateSfxArchive(archive_file_name);
//else
//    ExtractArchive();

const string data_file_name = "data.zip";
const string exe_file_name = "data.exe";

var data_file = new FileInfo(data_file_name);

if (!data_file.Exists)
    throw new FileNotFoundException($"Файл {data_file_name} не найден.");

var exe_file = CreateSfxArchive(data_file_name);



Console.WriteLine("End.");

return;

static string GetCurrentAppFilePath() => Path.Combine(AppContext.BaseDirectory, $"{AppDomain.CurrentDomain.FriendlyName}.exe");

static FileInfo CreateSfxArchive(string ZipFilePath)
{
    var sfx_file_path = Path.ChangeExtension(ZipFilePath, ".exe");

    using var sfx_file_stream = File.Create(sfx_file_path);
    var exe_file_location = GetCurrentAppFilePath();
    using var current_exe_stream = File.OpenRead(exe_file_location);

    current_exe_stream.CopyTo(sfx_file_stream);

    using var zip_file_stream = File.OpenRead(ZipFilePath);
    zip_file_stream.CopyTo(sfx_file_stream);

    Console.WriteLine($"SFX-архив создан: {sfx_file_path}");

    return new(sfx_file_path);
}

static void ExtractArchive()
{
    var exe_path = GetCurrentAppFilePath();

    using var exe_stream = File.OpenRead(exe_path);
    if (FindEndOfCentralDirectory(exe_stream) is not { } eocd)
    {
        Console.WriteLine("Архив не найден.");
        return;
    }

    // Позиционируемся на начало центрального каталога (абсолютное смещение)
    var cd_start_abs = eocd.CentralDirectoryAbsoluteStart;
    exe_stream.Position = cd_start_abs;
    using var cd_stream = new SubReadStream(exe_stream, eocd.CentralDirectorySize);

    var zip_file_structure = new ZipFileStructure(cd_stream);
    var central_directory = zip_file_structure.ReadCentralDirectory();

    // Распаковка файлов согласно центральному каталогу
    foreach (var entry in central_directory.Entries)
        ExtractFile(exe_stream, entry, eocd.ZipBaseOffset);
}

static EndOfCentralDirectory? FindEndOfCentralDirectory(Stream ExeStream)
{
    // Поиск EOCD (0x06054b50) с конца потока, максимально 64К + фиксированный размер
    const uint eocd_signature = 0x06054b50;
    const int max_comment = 0xFFFF;
    const int eocd_fixed = 22; // минимальный размер EOCD без комментария

    var search_size = (int)Math.Min(ExeStream.Length, max_comment + eocd_fixed);
    var search_start = ExeStream.Length - search_size;
    ExeStream.Position = search_start;

    var buffer = new byte[search_size];
    _ = ExeStream.Read(buffer, 0, buffer.Length);

    for (var i = buffer.Length - eocd_fixed; i >= 0; i--)
    {
        if (MemoryMarshal.Read<uint>(buffer.AsSpan(i)) != eocd_signature)
            continue;

        var span = buffer.AsSpan(i);
        var disk_no = MemoryMarshal.Read<ushort>(span.Slice(4));
        var cd_disk_no = MemoryMarshal.Read<ushort>(span.Slice(6));
        var disk_entries = MemoryMarshal.Read<ushort>(span.Slice(8));
        var total_entries = MemoryMarshal.Read<ushort>(span.Slice(10));
        var cd_size = MemoryMarshal.Read<uint>(span.Slice(12));
        var cd_offset = MemoryMarshal.Read<uint>(span.Slice(16));
        var comment_len = MemoryMarshal.Read<ushort>(span.Slice(20));

        // Поддерживаем только однотомные архивы
        if (disk_no != 0 || cd_disk_no != 0 || disk_entries != total_entries)
            return null;

        var eocd_abs = search_start + i; // абсолютная позиция EOCD в файле
        var cd_end_abs = eocd_abs;       // центральный каталог заканчивается перед EOCD
        var cd_start_abs = cd_end_abs - cd_size;
        var zip_base = cd_start_abs - cd_offset; // смещение начала zip-потока в SFX-файле

        return new EndOfCentralDirectory
        {
            CentralDirectorySize = cd_size,
            CentralDirectoryOffset = cd_offset,
            CommentLength = comment_len,
            EocdAbsoluteOffset = eocd_abs,
            CentralDirectoryAbsoluteStart = cd_start_abs,
            ZipBaseOffset = zip_base
        };
    }

    return null;
}

static void ExtractFile(Stream ZipStream, ZipCentralDirectoryEntry entry, long BaseOffset)
{
    // Создание каталогов при необходимости
    if (entry.FileName.EndsWith('/'))
    {
        Directory.CreateDirectory(entry.FileName);
        return;
    }

    // Переход к локальному заголовку и чтение его для получения смещения данных
    var local_header_abs = BaseOffset + entry.LocalHeaderOffset;
    ZipStream.Position = local_header_abs;
    var local = new ZipLocalFileHeader();
    local.Read(ZipStream);

    var data_start = local_header_abs + local.DataStartRelativeOffset;

    ZipStream.Position = data_start;

    // Извлечение: поддерживаем методы 0 (Stored) и 8 (Deflate)
    var file_dir = Path.GetDirectoryName(entry.FileName);
    if (!string.IsNullOrEmpty(file_dir))
        Directory.CreateDirectory(file_dir);

    using var file_stream = File.Create(entry.FileName);
    var crc = new CustomCRC();

    if (entry.CompressionMethod == 0)
    {
        CopyLimitedWithCrc(ZipStream, file_stream, entry.CompressedSize, crc);
    }
    else if (entry.CompressionMethod == 8)
    {
        using var limited = new SubReadStream(ZipStream, entry.CompressedSize);
        using var deflate = new DeflateStream(limited, CompressionMode.Decompress, leaveOpen: false);
        CopyAllWithCrc(deflate, file_stream, crc);
    }
    else
    {
        Console.WriteLine($"Метод сжатия {entry.CompressionMethod} не поддерживается для файла {entry.FileName}");
    }

    var actual_crc = crc.FinalizeHash();
    if (actual_crc != entry.Crc32)
        throw new InvalidDataException($"CRC mismatch for {entry.FileName}: expected 0x{entry.Crc32:X8}, actual 0x{actual_crc:X8}");

    Console.WriteLine($"Файл распакован: {entry.FileName}");
}

static void CopyLimitedWithCrc(Stream Source, Stream Destination, uint Count, CustomCRC Crc)
{
    var buffer = new byte[8192];
    var remaining = (long)Count;
    while (remaining > 0)
    {
        var to_read = (int)Math.Min(buffer.Length, remaining);
        var read = Source.Read(buffer, 0, to_read);
        if (read <= 0) break;
        Destination.Write(buffer, 0, read);
        Crc.Update(buffer, 0, read);
        remaining -= read;
    }
}

static void CopyAllWithCrc(Stream Source, Stream Destination, CustomCRC Crc)
{
    var buffer = new byte[8192];
    int read;
    while ((read = Source.Read(buffer, 0, buffer.Length)) > 0)
    {
        Destination.Write(buffer, 0, read);
        Crc.Update(buffer, 0, read);
    }
}

/// <summary>Структура EOCD</summary>
file struct EndOfCentralDirectory
{
    public uint CentralDirectorySize;
    public uint CentralDirectoryOffset;
    public ushort CommentLength;
    public long EocdAbsoluteOffset;
    public long CentralDirectoryAbsoluteStart;
    public long ZipBaseOffset;
}

/// <summary>Поток для чтения ограниченного диапазона базового потока</summary>
file sealed class SubReadStream : Stream
{
    private readonly Stream _Base;
    private readonly long _Start;
    private readonly long _Length;
    private long _Position;

    public SubReadStream(Stream Base, uint Length)
    {
        _Base = Base;
        _Start = Base.Position;
        _Length = Length;
        _Position = 0;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _Length;
    public override long Position { get => _Position; set => Seek(value, SeekOrigin.Begin); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var remaining = _Length - _Position;
        if (remaining <= 0) return 0;
        var to_read = (int)Math.Min(count, remaining);
        _Base.Position = _Start + _Position;
        var read = _Base.Read(buffer, offset, to_read);
        _Position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var new_pos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _Position + offset,
            SeekOrigin.End => _Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        if (new_pos < 0 || new_pos > _Length)
            throw new IOException("Выход за пределы поддиапазона");
        _Position = new_pos;
        return _Position;
    }

    public override void Flush() => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

