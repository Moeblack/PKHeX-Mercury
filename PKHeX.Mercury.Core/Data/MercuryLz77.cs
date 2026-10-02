namespace PKHeX.Mercury.Core;

/// <summary>
/// GBA BIOS type-0x10 LZ77 decompressor (as used by this ROM's sprite/palette tables).
/// Verified byte-for-byte against the extracted <c>out/sprites/bin</c> research output.
/// </summary>
internal static class MercuryLz77
{
    private const byte Header = 0x10;
    private const int MaxOutput = 0x100000;

    /// <summary>
    /// Decompresses an LZ77 block whose header starts at <paramref name="gbaAddress"/>.
    /// </summary>
    public static bool TryDecompress(byte[] rom, uint gbaAddress, out byte[] data)
    {
        data = [];
        long off = MercuryRomLayout.ToOffset(gbaAddress);
        if (off < 4 || off + 4 > rom.Length)
            return false;
        if (rom[off] != Header)
            return false;

        int size = rom[off + 1] | (rom[off + 2] << 8) | (rom[off + 3] << 16);
        if (size <= 0 || size > MaxOutput)
            return false;

        var result = new byte[size];
        int written = 0;
        long i = off + 4;

        while (written < size)
        {
            if (i >= rom.Length)
                return false;
            byte flags = rom[i++];
            for (int bit = 0; bit < 8 && written < size; bit++)
            {
                if ((flags & (0x80 >> bit)) != 0)
                {
                    // back-reference: 2 bytes -> length = high nibble + 3, distance = 12-bit + 1
                    if (i + 2 > rom.Length)
                        return false;
                    byte b0 = rom[i];
                    byte b1 = rom[i + 1];
                    i += 2;
                    int length = (b0 >> 4) + 3;
                    int distance = (((b0 & 0x0F) << 8) | b1) + 1;
                    if (distance > written)
                        return false;
                    if (written + length > size)
                        return false;
                    int start = written - distance;
                    for (int k = 0; k < length; k++)
                        result[written + k] = result[start + k];
                    written += length;
                }
                else
                {
                    if (i >= rom.Length)
                        return false;
                    result[written++] = rom[i++];
                }
            }
        }

        data = result;
        return true;
    }
}
