namespace ZipSFX;

/// <summary>Подсчёт CRC-32 (полиномиал 0xEDB88320, IEEE 802.3)</summary>
internal sealed class CustomCRC
{
    /// <summary>Таблица для быстрого подсчёта</summary>
    private static readonly uint[] __Table = CreateTable();

    /// <summary>Текущее значение CRC</summary>
    private uint _Value = 0xFFFFFFFFu;

    /// <summary>Обновляет CRC данными буфера</summary>
    /// <param name="Buffer">Буфер данных</param>
    /// <param name="Offset">Смещение</param>
    /// <param name="Count">Количество байт</param>
    public void Update(byte[] Buffer, int Offset, int Count)
    {
        var crc = _Value;
        var end = Offset + Count;
        for (var i = Offset; i < end; i++)
        {
            crc = __Table[(crc ^ Buffer[i]) & 0xFF] ^ (crc >> 8);
        }
        _Value = crc;
    }

    /// <summary>Возвращает финальное значение CRC-32</summary>
    public uint FinalizeHash() => _Value ^ 0xFFFFFFFFu;

    /// <summary>Сбрасывает состояние</summary>
    public void Reset() => _Value = 0xFFFFFFFFu;

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        const uint poly = 0xEDB88320u;
        for (uint i = 0; i < table.Length; i++)
        {
            var crc = i;
            for (var j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ poly : crc >> 1;
            table[i] = crc;
        }
        return table;
    }
}
