using System;
using System.Buffers.Binary;

namespace PKHeX.Mercury.Core;

/// <summary>Indexed front-buffer operation consumed by 0x08043458 for mapped resource 0x134.</summary>
internal static class MercuryPidSpots
{
    internal const int ResourceIndex = 0x134;

    // 0x0825265C, four 36-byte records: x/y, sixteen LE mask rows, two unused trailing bytes.
    // Identical in the verified Mercury 1.0 and 1.1 ROMs. Row bits run left to right, LSB first.
    internal static ReadOnlySpan<byte> MaskData =>
    [
        0x10, 0x07, 0x70, 0x00, 0xFC, 0x01, 0xFE, 0x03, 0xFE, 0x07, 0xFF, 0x07, 0xFF, 0x0F, 0xFF, 0x0F,
        0xFF, 0x0F, 0xFE, 0x07, 0xFE, 0x07, 0xFC, 0x03, 0xE0, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x28, 0x08, 0xE0, 0x01, 0xF8, 0x03, 0xFC, 0x07, 0xFE, 0x0F, 0xFE, 0x0F, 0xFF, 0x1F, 0xFF, 0x1F,
        0xFF, 0x1F, 0xFE, 0x0F, 0xFE, 0x0F, 0xFC, 0x07, 0xF8, 0x07, 0xE0, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x16, 0x19, 0x1C, 0x00, 0x3E, 0x00, 0x7F, 0x00, 0x7F, 0x00, 0x7F, 0x00, 0x7F, 0x00, 0x7F, 0x00,
        0x3E, 0x00, 0x1C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x22, 0x1A, 0x3C, 0x00, 0x7E, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00,
        0x7E, 0x00, 0x3C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
    ];

    /// <summary>
    /// Applies once to the entire decompressed tile buffer, before frame selection or palette lookup.
    /// Unsigned coordinate wrap matches the consumer. Out-of-buffer writes are skipped: this is a
    /// projection onto existing resource bytes, not a simulation of uninitialized 8192-byte game RAM.
    /// The caller must own <paramref name="tiles"/>; cached pack payloads must be cloned first.
    /// </summary>
    internal static void Apply(Span<byte> tiles, int resourceIndex, uint pid)
    {
        if (resourceIndex != ResourceIndex)
            return;

        var masks = MaskData;
        for (int spot = 0; spot < 4; spot++, pid >>= 8)
        {
            int record = spot * 36;
            byte x = unchecked((byte)(masks[record] + (pid & 15) - 8));
            byte y = unchecked((byte)(masks[record + 1] + ((pid >> 4) & 15) - 8));
            for (int row = 0; row < 16; row++, y = unchecked((byte)(y + 1)))
            {
                int bits = BinaryPrimitives.ReadUInt16LittleEndian(masks.Slice(record + 2 + (row * 2), 2));
                for (int column = x; column < x + 16; column++, bits >>= 1)
                {
                    if ((bits & 1) == 0)
                        continue;
                    int offset = ((column / 8) * 32) + ((column % 8) / 2) + ((y / 8) * 256) + ((y % 8) * 4);
                    if ((uint)offset >= (uint)tiles.Length)
                        continue;
                    int shift = (column & 1) * 4;
                    int index = (tiles[offset] >> shift) & 15;
                    if (index is >= 1 and <= 3)
                        tiles[offset] += (byte)(4 << shift);
                }
            }
        }
    }
}
