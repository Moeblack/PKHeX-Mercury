using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

/// <summary>
/// 全局运行期状态：资料（ROM/研究/Profile）与文字编码器。
/// 所有缓存写入 LocalApplicationData/PKHeX-Mercury/profile，绝不写源码目录。
/// </summary>
internal static class AppState
{
    public static string ProfileDirectory { get; private set; } = string.Empty;

    /// <summary>当前数据源（数字ID 或 ROM/研究/Profile）。</summary>
    public static MercuryGameData Data { get; private set; } = MercuryGameData.NumericOnly();

    /// <summary>用户单独导入的字表编码器；优先于资料自带编码器。</summary>
    public static MercuryTextCodec? ImportedCodec { get; set; }

    /// <summary>数据来源的显示文本。</summary>
    public static string DataSourceLabel { get; private set; } = "无 ROM 数据（仅数字 ID）";

    /// <summary>是否具备 ROM 基础数值（成长/学习表/图片等）。</summary>
    public static bool HasRomData { get; private set; }

    /// <summary>是否具备成长表（GetLevel/GetExperience 可用）；由 MercuryDataCore 提供。</summary>
    public static bool HasGrowthTables => HasRomData && Data.HasGrowthTables;

    public static MercuryTextCodec Codec => ImportedCodec ?? Data.Text;

    private static string CharmapPath => Path.Combine(ProfileDirectory, "mercury-charmap.json");

    public static void Initialize()
    {
        ProfileDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PKHeX-Mercury",
            "profile");

        TryLoadImportedCharmap();

        if (!Directory.Exists(ProfileDirectory))
            return;

        try
        {
            var data = MercuryGameData.LoadProfile(ProfileDirectory);
            Apply(data, "本地资料缓存（LocalApplicationData/PKHeX-Mercury/profile）");
        }
        catch
        {
            // 缓存损坏或版本不符：静默回退到数字模式，用户可重新导入。
        }
    }

    /// <summary>导入字表（game_data.js / charmap json）：用于物种/招式/道具/特性中文名，并缓存到用户本地目录。</summary>
    public static void SetImportedCharmap(string json)
    {
        var codec = MercuryTextCodec.FromCharmapJson(json);
        ImportedCodec = codec;

        // 仅设置昵称编码器不够：必须让资料本身用新字表重建物种/招式/道具/特性名称。
        try
        {
            Data = Data.WithTextCodec(codec);
        }
        catch
        {
            // 资料不支持替换字表时，至少保证昵称/OT 名使用新字表。
        }

        try
        {
            Directory.CreateDirectory(ProfileDirectory);
            File.WriteAllText(CharmapPath, json);
        }
        catch
        {
            // 缓存失败不影响本次使用。
        }

        // 有 ROM 资料时保存 profile（DataCore 会在 profile 内保留本地 rom-cache.gba 以恢复 sprites）。
        if (HasRomData)
            TryCacheProfile();
    }

    private static void TryLoadImportedCharmap()
    {
        try
        {
            if (File.Exists(CharmapPath))
                ImportedCodec = MercuryTextCodec.FromCharmapJson(File.ReadAllText(CharmapPath));
        }
        catch
        {
            // 忽略损坏的字表缓存。
        }
    }

    public static void Apply(MercuryGameData data, string sourceLabel)
    {
        // 已有导入字表时，应用到新资料，避免重启后被 profile 自带字表覆盖。
        if (ImportedCodec is not null)
        {
            try
            {
                data = data.WithTextCodec(ImportedCodec);
            }
            catch
            {
                // 资料不支持替换字表时保持原样。
            }
        }

        Data = data;
        HasRomData = !string.IsNullOrEmpty(data.RomSha256);
        DataSourceLabel = sourceLabel;
    }

    /// <summary>把当前资料缓存到用户本地目录（失败不抛出，不影响已加载数据）。</summary>
    public static void TryCacheProfile()
    {
        if (!HasRomData || string.IsNullOrEmpty(ProfileDirectory))
            return;

        try
        {
            Directory.CreateDirectory(ProfileDirectory);
            Data.SaveProfile(ProfileDirectory);
        }
        catch
        {
            // 缓存失败不影响使用。
        }
    }

    public static string ShaShort()
    {
        var sha = Data.RomSha256;
        return string.IsNullOrEmpty(sha) ? "无" : (sha.Length > 12 ? sha[..12] : sha);
    }
}
