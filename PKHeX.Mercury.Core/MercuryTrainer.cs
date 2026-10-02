namespace PKHeX.Mercury.Core;

/// <summary>
/// Mercury 1.1 trainer data. Offsets are from SaveBlock2 (name, gender, id, play time, money key)
/// and SaveBlock1 (money, coins); see docs/mercury-save-format.md.
/// </summary>
public sealed class MercuryTrainer
{
    private readonly byte[] _name;

    public MercuryTrainer()
    {
        _name = new byte[MercurySaveLayout.TrainerNameLength];
    }

    /// <summary>Player name bytes (SaveBlock2+0x00, 8 bytes, 0xFF-terminated in game text).</summary>
    public byte[] NameBytes
    {
        get => (byte[])_name.Clone();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length > MercurySaveLayout.TrainerNameLength)
                throw new ArgumentException($"Name must be at most {MercurySaveLayout.TrainerNameLength} bytes.", nameof(value));
            System.Array.Clear(_name);
            System.Buffer.BlockCopy(value, 0, _name, 0, value.Length);
        }
    }

    /// <summary>SaveBlock2+0x08 (0 = male, 1 = female).</summary>
    public byte Gender { get; set; }

    /// <summary>Combined trainer id, <c>(SID &lt;&lt; 16) | TID</c>, from SaveBlock2+0x0A/+0x0C.</summary>
    public uint ID32 { get; set; }

    /// <summary>SaveBlock1+0x290, stored XOR the SaveBlock2+0xF20 key (ROM GetMoney 0x0809FD58).</summary>
    public uint Money { get; set; }

    /// <summary>
    /// Coins, u32 at section 13 offset 0x7CC (RAM 0x0203B814 = parasite block 0x0203B498 + 0x37C).
    /// Consumers 0x09D59F8C (Get), 0x09D59F98 (Set), 0x09D59FA4 (Add, capped at 0x3B9AC9FF). Not XORed.
    /// </summary>
    public uint Coins { get; set; }

    /// <summary>SaveBlock2+0x0E (u16).</summary>
    public ushort PlayedHours { get; set; }

    /// <summary>SaveBlock2+0x10.</summary>
    public byte PlayedMinutes { get; set; }

    /// <summary>SaveBlock2+0x11.</summary>
    public byte PlayedSeconds { get; set; }

    public MercuryTrainer Clone() => new()
    {
        NameBytes = (byte[])_name.Clone(),
        Gender = Gender,
        ID32 = ID32,
        Money = Money,
        Coins = Coins,
        PlayedHours = PlayedHours,
        PlayedMinutes = PlayedMinutes,
        PlayedSeconds = PlayedSeconds,
    };

    internal byte[] NameBytesRaw => _name;
}
