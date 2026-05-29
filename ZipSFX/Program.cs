using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Diagnostics;
using ZipSFX;

// ------------------------------------------------------------
// Универсальный SFX-модуль
// Режимы запуска:
// 1) <exe> <path>.zip           — создать SFX-архив: [модуль] + [zip]
// 2) <exe> [extractPath?]       — распаковать присоединённый архив в каталог (текущий, если не указан)
// ------------------------------------------------------------

// Точка входа: разбираем аргументы командной строки
if (args is [{ Length: > 0 } arg0, ..])
{
    // Если указан существующий файл с расширением .zip — выполняем слияние
    if (File.Exists(arg0) && string.Equals(Path.GetExtension(arg0), ".zip", StringComparison.OrdinalIgnoreCase))
    {
        CreateSfxArchive(arg0);
        return;
    }

    // Если указан существующий каталог — распаковываем в него
    if (Directory.Exists(arg0))
    {
        ExtractArchive(arg0);
        return;
    }

    // Если указан путь к файлу, но он не .zip — сообщаем об ошибке
    if (File.Exists(arg0))
    {
        Console.WriteLine($"Файл {arg0} существует и не является zip-архивом.");
        return;
    }

    // Иначе трактуем аргумент как путь назначения распаковки (включая несуществующие каталоги)
    ExtractArchive(arg0);
    return;
}

// Без аргументов: если архив присоединён — распаковываем в текущий каталог
if (HasAttachedArchive())
{
    ExtractArchive(Environment.CurrentDirectory);
    return;
}

const string? hint_str = """
 Аргументы не указаны и архив не присоединён. Нечего делать.
 ------------------------------------------------------------
 Универсальный SFX-модуль
 Режимы запуска:
 1) <exe> <path>.zip           — создать SFX-архив: [модуль] + [zip]
 2) <exe> [extractPath?]       — распаковать присоединённый архив в каталог (текущий, если не указан)
 ------------------------------------------------------------
 """;
Console.WriteLine(hint_str);
return;

///// <summary>Возвращает путь к текущему исполняемому файлу</summary>
static string GetCurrentAppFilePath()
{
    // Попробуем использовать Environment.ProcessPath (доступно в новых рантаймах)
    try
    {
        if (!string.IsNullOrEmpty(Environment.ProcessPath))
            return Environment.ProcessPath;
    }
    catch { }

    try
    {
        using var proc = Process.GetCurrentProcess();
        var path = proc.MainModule?.FileName;
        if (!string.IsNullOrEmpty(path))
            return path;
    }
    catch { }

    // Фоллбэк: предполагаем имя exe в AppContext.BaseDirectory
    var friendly = AppDomain.CurrentDomain.FriendlyName;
    var suffix = RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? ".exe" : string.Empty;
    return Path.Combine(AppContext.BaseDirectory, friendly + suffix);
}

///// <summary>Определяет, присоединён ли к текущему exe архив</summary>
static bool HasAttachedArchive()
{
    var exe_path = GetCurrentAppFilePath();
    using var exe_stream = File.OpenRead(exe_path);
    return FindEndOfCentralDirectory(exe_stream) is not null;
}

///// <summary>Создаёт SFX-архив: [модуль] + [zip]</summary>
///// <param name="ZipFilePath">Путь к исходному zip-файлу</param>
static FileInfo CreateSfxArchive(string ZipFilePath)
{
    if (!File.Exists(ZipFilePath))
        throw new FileNotFoundException($"Файл {ZipFilePath} не найден.");

    var output_path = Path.ChangeExtension(ZipFilePath, ".exe");

    // 1) Определяем длину SFX-модуля в текущем exe (исключая присоединённый zip, если он есть)
    var exe_path = GetCurrentAppFilePath();
    long sfx_length; // длина области модуля в текущем exe
    using (var exe_stream = File.OpenRead(exe_path))
        sfx_length = GetSfxModuleLength(exe_stream);

    // 2) Записываем в выходной файл: [модуль] + [zip]
    using var out_stream = File.Create(output_path);

    // Копируем модуль
    using (var exe_stream = File.OpenRead(exe_path))
        CopyExact(exe_stream, out_stream, sfx_length);

    // Копируем содержимое zip-файла
    using (var zip_stream = File.OpenRead(ZipFilePath))
        zip_stream.CopyTo(out_stream);

    Console.WriteLine($"SFX-архив создан: {output_path}");
    return new(output_path);
}

///// <summary>Распаковывает присоединённый к текущему exe архив</summary>
///// <param name="DestinationDirectory">Каталог распаковки; если null или пустая строка — текущий каталог</param>
static void ExtractArchive(string? DestinationDirectory = null)
{
    var dest_dir = string.IsNullOrWhiteSpace(DestinationDirectory) ? Environment.CurrentDirectory : DestinationDirectory!;

    // Убеждаемся, что каталог существует
    Directory.CreateDirectory(dest_dir);

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
        ExtractFile(exe_stream, entry, eocd.ZipBaseOffset, dest_dir);
}

///// <summary>Вычисляет длину области SFX-модуля в заданном потоке exe</summary>
///// <param name="ExeStream">Поток текущего исполняемого файла</param>
///// <returns>Длина части файла, соответствующей самому модулю (без присоединённого zip)</returns>
static long GetSfxModuleLength(Stream ExeStream)
{
    // Если архив присоединён — длина модуля = смещение начала zip-потока (ZipBaseOffset)
    // Иначе — длина модуля = длина всего файла
    if (FindEndOfCentralDirectory(ExeStream) is { } eocd)
        return eocd.ZipBaseOffset;

    return ExeStream.Length;
}

///// <summary>Ищет EOCD (End Of Central Directory) в конце потока и возвращает его параметры</summary>
///// <param name="ExeStream">Поток файла (exe или zip)</param>
static EndOfCentralDirectory? FindEndOfCentralDirectory(Stream ExeStream)
{
    // Поиск EOCD (0x06054b50) с конца потока, максимально 64К + фиксированный размер
    const uint eocd_signature = 0x06054b50;
    const int max_comment = 0xFFFF;
    const int eocd_fixed = 22; // минимальный размер EOCD без комментария
    const int total_search_length = max_comment + eocd_fixed;

    var search_size = (int)Math.Min(ExeStream.Length, total_search_length);
    var search_start = ExeStream.Length - search_size;
    ExeStream.Position = search_start;

    var buffer_array = ArrayPool<byte>.Shared.Rent(search_size);
    var buffer = buffer_array.AsSpan(0, search_size);

    try
    {
        ExeStream.ReadExactly(buffer);

        for (var i = buffer.Length - eocd_fixed; i >= 0; i--)
        {
            if (i + 4 > buffer.Length) continue;
            var sig = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(i));
            if (sig != eocd_signature) continue;

            if (i + eocd_fixed > buffer.Length) continue;

            var disk_no = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(i + 4));
            var cd_disk_no = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(i + 6));
            var disk_entries = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(i + 8));
            var total_entries = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(i + 10));
            var cd_size = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(i + 12));
            var cd_offset = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(i + 16));
            var comment_len = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(i + 20));

            // Поддерживаем только однотомные архивы; при расхождении продолжаем поиск
            if (disk_no != 0 || cd_disk_no != 0 || disk_entries != total_entries)
                continue;

            var eocd_abs = search_start + i; // абсолютная позиция EOCD в файле
            var cd_end_abs = eocd_abs;       // центральный каталог заканчивается перед EOCD
            var cd_start_abs = cd_end_abs - cd_size;
            var zip_base = cd_start_abs - cd_offset; // смещение начала zip-потока в SFX-файле

            // Проверка валидности вычисленных смещений
            if (cd_start_abs < 0 || zip_base < 0) continue;
            if (cd_start_abs > ExeStream.Length || zip_base > ExeStream.Length) continue;

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
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer_array);
    }

    return null;
}

///// <summary>Распаковывает одну запись zip в указанный каталог</summary>
///// <param name="ZipStream">Поток, из которого читаются данные (exe с присоединённым zip)</param>
///// <param name="entry">Запись центрального каталога</param>
///// <param name="BaseOffset">Абсолютное смещение начала zip-потока</param>
///// <param name="DestinationRoot">Корневой каталог распаковки</param>
static void ExtractFile(Stream ZipStream, ZipCentralDirectoryEntry entry, long BaseOffset, string DestinationRoot)
{
    // Поддержка каталогов
    if (entry.FileName.EndsWith('/'))
    {
        var dir_path = Path.Combine(DestinationRoot, entry.FileName.Replace('/', Path.DirectorySeparatorChar));
        var fullDir = Path.GetFullPath(dir_path);
        var baseFull = Path.GetFullPath(DestinationRoot);
        if (!baseFull.EndsWith(Path.DirectorySeparatorChar)) baseFull += Path.DirectorySeparatorChar;
        if (!fullDir.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Пропускаем каталог с потенциально небезопасным путём: {entry.FileName}");
            return;
        }

        Directory.CreateDirectory(fullDir);
        return;
    }

    // Переход к локальному заголовку и чтение его для получения смещения данных
    var local_header_abs = BaseOffset + entry.LocalHeaderOffset;
    ZipStream.Position = local_header_abs;
    var local = new ZipLocalFileHeader();
    local.Read(ZipStream);

    var data_start = local_header_abs + local.DataStartRelativeOffset;

    ZipStream.Position = data_start;

    // Путь к файлу назначения
    var rel_path = entry.FileName.Replace('/', Path.DirectorySeparatorChar);
    var target_path = Path.Combine(DestinationRoot, rel_path);
    var fullTarget = Path.GetFullPath(target_path);
    var baseFullTarget = Path.GetFullPath(DestinationRoot);
    if (!baseFullTarget.EndsWith(Path.DirectorySeparatorChar)) baseFullTarget += Path.DirectorySeparatorChar;
    if (!fullTarget.StartsWith(baseFullTarget, StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Пропускаем распаковку файла с потенциально небезопасным путём: {entry.FileName}");
        return;
    }

    var file_dir = Path.GetDirectoryName(fullTarget);
    if (!string.IsNullOrEmpty(file_dir))
        Directory.CreateDirectory(file_dir);

    using var file_stream = File.Create(fullTarget);
    var crc = new CustomCRC();

    switch (entry.CompressionMethod)
    {
        case 0:
            CopyLimitedWithCrc(ZipStream, file_stream, entry.CompressedSize, crc);
            break;

        case 8:
            using (var limited = new SubReadStream(ZipStream, entry.CompressedSize))
            using (var deflate = new DeflateStream(limited, CompressionMode.Decompress, leaveOpen: false))
                CopyAllWithCrc(deflate, file_stream, crc);
            break;

        default:
            Console.WriteLine($"Метод сжатия {entry.CompressionMethod} не поддерживается для файла {entry.FileName}");
            break;
    }

    var actual_crc = crc.FinalizeHash();
    if (actual_crc != entry.Crc32)
        throw new InvalidDataException($"CRC mismatch for {entry.FileName}: expected 0x{entry.Crc32:X8}, actual 0x{actual_crc:X8}");

    Console.WriteLine($"Файл распакован: {target_path}");
}

///// <summary>Копирует из исходного потока ровно указанное количество байт</summary>
///// <param name="Source">Источник</param>
///// <param name="Destination">Назначение</param>
///// <param name="Count">Количество байт</param>
static void CopyExact(Stream Source, Stream Destination, long Count)
{
    const int buffer_size = 8192;
    var buffer_array = ArrayPool<byte>.Shared.Rent(buffer_size);
    var buffer = buffer_array.AsSpan(0, buffer_size);

    try
    {
        var remaining = Count;
        while (remaining > 0)
        {
            var to_read = (int)Math.Min(buffer.Length, remaining);
            var read = Source.Read(buffer[..to_read]);
            if (read <= 0)
                throw new EndOfStreamException("Неожиданный конец потока при копировании модуля");

            Destination.Write(buffer[..read]);
            remaining -= read;
        }
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer_array);
    }
}

///// <summary>Копирует ограниченное количество байт с подсчётом CRC-32</summary>
static void CopyLimitedWithCrc(Stream Source, Stream Destination, uint Count, CustomCRC Crc)
{
    const int buffer_size = 8192;
    var buffer_array = ArrayPool<byte>.Shared.Rent(buffer_size);
    var buffer = buffer_array.AsSpan(0, buffer_size);
    try
    {
        var remaining = (long)Count;

        while (remaining > 0)
        {
            var to_read = (int)Math.Min(buffer.Length, remaining);
            var read = Source.Read(buffer[..to_read]);
            if (read <= 0)
                throw new EndOfStreamException("Неожиданный конец потока при копировании файла");

            Destination.Write(buffer[..read]);
            Crc.Update(buffer[..read]);

            remaining -= read;
        }
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer_array);
    }
}

///// <summary>Копирует все данные из потока с подсчётом CRC-32</summary>
static void CopyAllWithCrc(Stream Source, Stream Destination, CustomCRC Crc)
{
    const int buffer_size = 8192;
    var buffer_array = ArrayPool<byte>.Shared.Rent(buffer_size);
    var buffer = buffer_array.AsSpan(0, buffer_size);
    try
    {
        int read;
        while ((read = Source.Read(buffer)) > 0)
        {
            // Используем Span для обновления CRC и записи
            Destination.Write(buffer[..read]);
            Crc.Update(buffer[..read]);
        }
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer_array);
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
file sealed class SubReadStream(Stream Base, uint Length) : Stream
{
    private readonly Stream _Base = Base;
    private readonly long _Start = Base.Position;
    private readonly long _Length = Length;
    private long _Position = 0;

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

