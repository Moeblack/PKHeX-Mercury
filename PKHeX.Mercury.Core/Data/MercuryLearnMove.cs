namespace PKHeX.Mercury.Core;

/// <summary>A single level-up learn entry: move id + level. Order follows the ROM learnset table.</summary>
public sealed record MercuryLearnMove(int Move, int Level);
