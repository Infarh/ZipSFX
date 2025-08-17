namespace ZipSFX;

/// <summary>Класс для работы со структурой zip-файла</summary>
/// <remarks>Инициализирует новый экземпляр класса ZipFileStructure</remarks>
/// <param name="data">Поток данных zip-файла</param>
internal class ZipFileStructure(Stream data)
{
    /// <summary>Читает центральный каталог из zip-файла</summary>
    /// <returns>Центральный каталог zip-файла</returns>
    public ZipCentralDirectory ReadCentralDirectory()
    {
        var central_directory = new ZipCentralDirectory();
        central_directory.Read(data);
        return central_directory;
    }
}