using ZipSFX;

if (args is [{ Length: > 0 } archive_file_name, ..])
    CreateSfxArchive(archive_file_name);
else
    ExtractArchive();

Console.WriteLine("End.");

return;

static string GetCurrentAppFilePath() => Path.Combine(AppContext.BaseDirectory, $"{AppDomain.CurrentDomain.FriendlyName}.exe");

static void CreateSfxArchive(string ZipFilePath)
{
    var sfx_file_path = Path.ChangeExtension(ZipFilePath, ".exe");

    using var sfx_file_stream = File.Create(sfx_file_path);
    var exe_file_location = GetCurrentAppFilePath();
    using var current_exe_stream = File.OpenRead(exe_file_location);

    current_exe_stream.CopyTo(sfx_file_stream);

    using var zip_file_stream = File.OpenRead(ZipFilePath);
    zip_file_stream.CopyTo(sfx_file_stream);

    Console.WriteLine($"SFX-архив создан: {sfx_file_path}");
}

static void ExtractArchive()
{
    var exe_path = GetCurrentAppFilePath();

    using var exe_stream = File.OpenRead(exe_path);
    var zip_start_offset = FindZipStartOffset(exe_stream);
    if (zip_start_offset < 0)
    {
        Console.WriteLine("Архив не найден.");
        return;
    }

    exe_stream.Seek(zip_start_offset, SeekOrigin.Begin);
    using var zip_stream = new MemoryStream();
    exe_stream.CopyTo(zip_stream);
    zip_stream.Seek(0, SeekOrigin.Begin);

    var zip_file_structure = new ZipFileStructure(zip_stream);
    var central_directory = zip_file_structure.ReadCentralDirectory();

    // Распаковка файлов из центрального каталога
    foreach (var entry in central_directory.Entries)
        ExtractFile(zip_stream, entry);
}

static long FindZipStartOffset(Stream ExeStream)
{
    // Поиск сигнатуры центрального каталога (0x02014b50)
    byte[] signature = [0x50, 0x4b, 0x03, 0x04];
    long offset = -1;

    for (var i = ExeStream.Length - 4; i >= 0; i--)
    {
        ExeStream.Seek(i, SeekOrigin.Begin);
        if (ExeStream.ReadByte() != signature[0] ||
            ExeStream.ReadByte() != signature[1] ||
            ExeStream.ReadByte() != signature[2] ||
            ExeStream.ReadByte() != signature[3])
            continue;

        offset = i;
        break;
    }

    return offset;
}

static void ExtractFile(Stream ZipStream, ZipCentralDirectoryEntry entry)
{
    ZipStream.Seek(entry.LocalHeaderOffset, SeekOrigin.Begin);

    using var file_stream = File.Create(entry.FileName);
    var buffer = new byte[8192];
    var remaining_bytes = entry.CompressedSize;
    int bytes_read;

    while (remaining_bytes > 0 && (bytes_read = ZipStream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining_bytes))) > 0)
    {
        file_stream.Write(buffer, 0, bytes_read);
        remaining_bytes -= bytes_read;
    }

    Console.WriteLine($"Файл распакован: {entry.FileName}");
}

