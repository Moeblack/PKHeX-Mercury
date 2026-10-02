namespace PKHeX.Mercury.Core;

/// <summary>
/// Stored-ability -> ability-name-pool alias resolution for the 96 (species, stored ability) pairs
/// where the ROM function <c>0x09CE94E8</c> returns a name-pool entry in 256..299 instead of the
/// default <c>base + 13 * ability</c> entry. Every pair here was evaluated from the ROM and matches
/// <c>out/ability_display_names.json</c> (3356 non-zero slots, 106 special slot records collapsing to
/// these 96 unique species/ability pairs). Slot is irrelevant: the ROM function only takes
/// (ability, species).
/// </summary>
internal static class MercuryAbilityAliases
{
    private static readonly Dictionary<(int Species, int Ability), int> Aliases = new(EntryCount)
    {
        [(21, 200)] = 291,
        [(22, 200)] = 291,
        [(31, 221)] = 268,
        [(52, 5)] = 278,
        [(53, 5)] = 278,
        [(56, 15)] = 257,
        [(57, 15)] = 257,
        [(67, 5)] = 279,
        [(68, 5)] = 279,
        [(77, 118)] = 281,
        [(78, 118)] = 281,
        [(124, 248)] = 295,
        [(125, 15)] = 257,
        [(126, 15)] = 257,
        [(166, 67)] = 276,
        [(192, 117)] = 289,
        [(203, 151)] = 288,
        [(219, 131)] = 280,
        [(225, 15)] = 257,
        [(236, 15)] = 257,
        [(238, 248)] = 295,
        [(239, 15)] = 257,
        [(240, 15)] = 257,
        [(264, 117)] = 273,
        [(266, 158)] = 274,
        [(294, 248)] = 296,
        [(318, 81)] = 284,
        [(319, 81)] = 284,
        [(321, 29)] = 258,
        [(333, 246)] = 294,
        [(334, 246)] = 294,
        [(340, 101)] = 261,
        [(356, 37)] = 259,
        [(357, 37)] = 259,
        [(358, 241)] = 292,
        [(359, 241)] = 292,
        [(365, 15)] = 257,
        [(370, 246)] = 293,
        [(371, 246)] = 293,
        [(372, 246)] = 293,
        [(379, 117)] = 290,
        [(406, 13)] = 256,
        [(463, 239)] = 298,
        [(464, 239)] = 298,
        [(467, 150)] = 285,
        [(469, 221)] = 268,
        [(469, 239)] = 286,
        [(517, 101)] = 261,
        [(519, 15)] = 257,
        [(524, 248)] = 295,
        [(559, 15)] = 257,
        [(572, 118)] = 283,
        [(573, 118)] = 283,
        [(574, 118)] = 283,
        [(575, 118)] = 282,
        [(576, 118)] = 282,
        [(617, 101)] = 261,
        [(618, 101)] = 261,
        [(650, 24)] = 260,
        [(651, 24)] = 260,
        [(696, 152)] = 262,
        [(697, 152)] = 263,
        [(703, 118)] = 283,
        [(752, 152)] = 263,
        [(753, 152)] = 262,
        [(776, 123)] = 299,
        [(822, 246)] = 294,
        [(823, 246)] = 294,
        [(831, 123)] = 299,
        [(897, 37)] = 259,
        [(957, 215)] = 297,
        [(961, 15)] = 257,
        [(980, 221)] = 268,
        [(984, 160)] = 267,
        [(994, 24)] = 260,
        [(1027, 121)] = 266,
        [(1028, 121)] = 266,
        [(1034, 234)] = 269,
        [(1035, 234)] = 269,
        [(1046, 15)] = 257,
        [(1105, 150)] = 265,
        [(1106, 150)] = 265,
        [(1107, 150)] = 265,
        [(1138, 244)] = 270,
        [(1139, 244)] = 270,
        [(1142, 29)] = 258,
        [(1143, 29)] = 258,
        [(1166, 101)] = 261,
        [(1188, 118)] = 264,
        [(1220, 15)] = 257,
        [(1275, 150)] = 265,
        [(1286, 29)] = 258,
        [(1406, 93)] = 277,
        [(1407, 22)] = 275,
        [(1471, 67)] = 276,
        [(1472, 67)] = 276,
    };

    private const int EntryCount = 96;

    /// <summary>
    /// Resolves a stored ability id for a species to a name-pool index.
    /// Ability 0 means "no ability" and resolves to pool index 0.
    /// </summary>
    public static int Resolve(int species, int ability)
    {
        if (ability <= 0)
            return 0;
        return Aliases.TryGetValue((species, ability), out int index) ? index : ability;
    }

    /// <summary>Number of alias rules.</summary>
    public static int Count => EntryCount;
}
