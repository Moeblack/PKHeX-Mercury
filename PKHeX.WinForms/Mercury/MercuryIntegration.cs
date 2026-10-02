using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using PKHeX.Core;
using PKHeX.Drawing;
using PKHeX.Drawing.PokeSprite;
using PKHeX.Mercury.Core;

namespace PKHeX.WinForms;

/// <summary>
/// Wires the Mercury (ROM hack) format into the original PKHeX WinForms shell.
/// <para>
/// Loads the user-local profile, registers the save reader before any file arguments are processed, and
/// swaps the active <see cref="GameStrings"/> data source and sprite source only while a Mercury save is open.
/// The original controls, handlers and retail save support are untouched.
/// </para>
/// </summary>
internal static class MercuryIntegration
{
    private static MercuryGameData? _data;
    private static MercurySaveReader? _reader;
    private static MercuryGameStringsFactory? _factory;

    /// <summary>
    /// Raised after the profile/data has been replaced. The host reopens the active Mercury save (if any)
    /// so controls do not keep a stale data source.
    /// </summary>
    public static event Action? ProfileChanged;

    /// <summary>User-local profile directory: %LOCALAPPDATA%/PKHeX-Mercury/profile.</summary>
    public static string ProfileDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PKHeX-Mercury", "profile");

    public static MercuryGameData? Data => _data;

    /// <summary>
    /// Loads the default profile (if present) and registers the format hooks. Must run before startup
    /// arguments are processed so that a Mercury save passed on the command line is recognized.
    /// </summary>
    public static void Initialize()
    {
        if (_factory is null)
        {
            _factory = new MercuryGameStringsFactory();
            GameInfo.SaveSpecificStringsFactory = _factory.Create;
            SpriteUtil.CustomSpriteSource = GetSprite;
            FileUtil.CustomEntityReader = TryReadEntity;
        }

        TryLoadDefaultProfile();
        TryApplyCachedCharmap();
    }

    /// <summary>Cached user-imported charmap (json / game_data.js payload), re-applied on every start.</summary>
    private static string CharmapPath => Path.Combine(ProfileDirectory, "mercury-charmap.json");

    private static void TryApplyCachedCharmap()
    {
        if (_data is null)
            return;
        try
        {
            if (!File.Exists(CharmapPath))
                return;
            var codec = MercuryTextCodec.FromCharmapJson(File.ReadAllText(CharmapPath));
            SetData(_data.WithTextCodec(codec));
        }
        catch
        {
            // Ignore a corrupted charmap cache; the profile's own codec remains.
        }
    }

    private static void TryLoadDefaultProfile()
    {
        try
        {
            if (!Directory.Exists(ProfileDirectory))
                return;
            var data = MercuryGameData.LoadProfile(ProfileDirectory);
            if (data.Species.Count == 0)
                return;
            SetData(data);
        }
        catch
        {
            // A broken/absent profile must not prevent the retail editor from starting.
        }
    }

    private static void SetData(MercuryGameData data)
    {
        MercuryEncounterContext.Clear();
        _data = data;
        MercuryPKM.DefaultGameData = data; // required by native blank-entity creation paths

        if (_reader is not null)
            SaveUtil.CustomSaveReaders.Remove(_reader);
        _reader = new MercurySaveReader(data);
        SaveUtil.CustomSaveReaders.Insert(0, _reader);
    }

    private static void Publish(MercuryGameData data)
    {
        SetData(data);
        ProfileChanged?.Invoke();
    }

    // ------------------------------------------------------------------ menu

    /// <summary>
    /// Adds the Mercury configuration submenu to the original native Tools menu.
    /// </summary>
    public static void AddMenuControls(ToolStripMenuItem tools)
    {
        var root = new ToolStripMenuItem { Name = "Menu_Mercury", Text = "Mercury" };
        AddItem(root, "Menu_MercurySetup", "Mercury ROM/profile setup...", async (s, _) => await ConfigureFromRom(Owner(s), s as ToolStripMenuItem));
        AddItem(root, "Menu_MercuryImportCharmap", "Import name charmap (JSON)...", (s, _) => ImportCharmap(Owner(s)));
        AddItem(root, "Menu_MercuryDownloadCharmap", "Download public HOME charmap...", async (s, _) => await DownloadCharmapAsync(Owner(s)));
        AddItem(root, "Menu_MercuryImportResearch", "Import research directory...", (s, _) => ImportResearch(Owner(s)));
        var encounterEvidence = new ToolStripMenuItem
        {
            Name = "Menu_MercuryImportEncounterEvidence",
            Text = L("EncounterEvidence.Menu", "Import encounter evidence (JSON)..."),
        };
        encounterEvidence.Click += (s, _) => ImportEncounterEvidence(Owner(s));
        root.DropDownItems.Add(encounterEvidence);
        root.DropDownOpening += (_, _) => encounterEvidence.Text = L("EncounterEvidence.Menu", "Import encounter evidence (JSON)...");
        AddItem(root, "Menu_MercuryLoadProfile", "Load existing profile folder...", (s, _) => LoadProfileFolder(Owner(s)));
        tools.DropDownItems.Add(root);
    }

    private static void AddItem(ToolStripMenuItem parent, string name, string text, EventHandler handler)
    {
        var item = new ToolStripMenuItem { Name = name, Text = text };
        item.Click += handler;
        parent.DropDownItems.Add(item);
    }

    private static IWin32Window? Owner(object? sender)
        => (sender as ToolStripItem)?.GetCurrentParent()?.FindForm();

    /// <summary>Localized text through the native translation mechanism (falls back to the given default).</summary>
    private static string L(string key, string fallback)
        => WinFormsTranslator.TranslateText($"Mercury.{key}", fallback, Main.CurrentLanguage);

    /// <summary>Explicit, session-only import; cancellation or failure never replaces existing evidence.</summary>
    public static void ImportEncounterEvidence(IWin32Window? owner)
    {
        var data = _data;
        if (!MercuryEncounterContext.CanImport(data))
        {
            WinFormsUtil.Error(L("EncounterEvidence.NeedData", "Configure the supported Mercury ROM/profile with a verified ROM cache before importing encounter evidence."));
            return;
        }

        using var ofd = new OpenFileDialog
        {
            Filter = L("EncounterEvidence.Filter", "Encounter evidence (*.json)|*.json|All files (*.*)|*.*"),
            Title = L("EncounterEvidence.SelectFile", "Import Mercury encounter evidence JSON"),
        };
        if (ofd.ShowDialog(owner) != DialogResult.OK)
            return;
        if (!ReferenceEquals(data, _data))
        {
            WinFormsUtil.Error(L("EncounterEvidence.DataChanged", "Mercury data changed while choosing the file. Import again for the current data."));
            return;
        }

        try
        {
            var evidence = MercuryEncounterContext.Import(data!, ofd.FileName);
            if (evidence is null)
                return;
            int ordinary = 0;
            int dynamic = 0;
            foreach (var record in evidence.Records)
            {
                if (record.Source == MercuryEncounterSource.OrdinaryTable)
                    ordinary++;
                else
                    dynamic++;
            }
            WinFormsUtil.Alert(L("EncounterEvidence.Imported", "Encounter evidence imported for this session."),
                string.Format(L("EncounterEvidence.Counts", "Ordinary slots: {0}; dynamic records: {1}."), ordinary, dynamic),
                L("EncounterEvidence.Unknown", "Source conditions remain undetermined. A matching SHA declaration is not full ROM-table verification; source legality remains Unknown."));
        }
        catch (Exception e)
        {
            WinFormsUtil.Error(L("EncounterEvidence.Failed", "Encounter evidence import failed. Previously imported evidence has not been replaced."), e);
        }
    }

    /// <summary>
    /// Prompts for the exact Mercury ROM and stores a verified local profile.
    /// </summary>
    public static async Task ConfigureFromRom(IWin32Window? owner, ToolStripMenuItem? item = null)
    {
        using var ofd = new OpenFileDialog
        {
            Filter = L("RomFilter", "GBA ROM (*.gba)|*.gba|All files (*.*)|*.*"),
            Title = L("SetupSelectRom", "Select the Mercury ROM (downloads the public HOME charmap if needed)"),
        };
        if (ofd.ShowDialog(owner) != DialogResult.OK)
            return;

        using var progress = new Form
        {
            Text = L("SetupWorking", "Mercury setup in progress"),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(480, 110),
            MaximizeBox = false,
            MinimizeBox = false,
            ControlBox = false,
            ShowInTaskbar = false,
        };
        progress.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Text = L("SetupWorkingDetail", "Reading your ROM and preparing the profile. If no imported charmap is available, the public HOME charmap is downloaded (30-second timeout)."),
        });
        progress.Controls.Add(new ProgressBar { Dock = DockStyle.Bottom, Style = ProgressBarStyle.Marquee });
        MercuryGameData? data;
        if (item is not null)
            item.Enabled = false;
        try
        {
            progress.Show(owner);
            data = await MercuryDataSetup.ConfigureFromRomAsync(ofd.FileName, ExistingCodec(), ProfileDirectory);
        }
        catch (Exception e)
        {
            progress.Hide();
            WinFormsUtil.Error(L("SetupFailed", "Setup failed. See the error below. For charmap download failures, use Import name charmap, then select your ROM again. The previously active data has not been replaced."), e);
            return;
        }
        finally
        {
            progress.Close();
            if (item is not null)
                item.Enabled = true;
        }
        if (data is null)
            return;
        Publish(data);
        WinFormsUtil.Alert(L("SetupDone", "ROM profile and name charmap saved."), $"种类 {data.Species.Count} / 招式 {data.Moves.Count} / 道具 {data.Items.Count}");
    }

    /// <summary>Backwards-compatible alias used by documentation/tooling.</summary>
    public static Task ConfigureProfile(IWin32Window? owner) => ConfigureFromRom(owner);

    /// <summary>
    /// Imports a name charmap JSON and re-decodes the current data with it (best effort: keeps numeric rules).
    /// </summary>
    public static void ImportCharmap(IWin32Window? owner)
    {
        var baseData = _data ?? TryLoadDefaultProfileData();
        using var ofd = new OpenFileDialog
        {
            Filter = L("CharmapFilter", "Charmap/game_data (*.json;*.js;*.html)|*.json;*.js;*.html|All files (*.*)|*.*"),
            Title = L("SelectCharmap", "Select a name charmap JSON"),
        };
        if (ofd.ShowDialog(owner) != DialogResult.OK)
            return;

        try
        {
            var text = File.ReadAllText(ofd.FileName);
            var codec = MercuryDataSetup.ParseCharmap(text);
            var updated = baseData?.WithTextCodec(codec);
            CacheCharmap(text);
            if (updated is null)
            {
                WinFormsUtil.Alert(L("CharmapSaved", "Name charmap saved. Select your ROM to complete setup."));
                return;
            }
            Publish(updated);
            WinFormsUtil.Alert(L("CharmapImported", "字符映射已导入。"), $"单字节 {codec.SingleByteCount} / 双字节 {codec.DoubleByteCount}");
        }
        catch (Exception e)
        {
            WinFormsUtil.Error(L("CharmapInvalid", "字符映射导入失败。"), e);
        }
    }

    private const string HomeCharmapUrl = MercuryDataSetup.HomeCharmapUrl;

    /// <summary>
    /// Downloads the user-visible public HOME charmap page and imports its embedded payload through the same
    /// <see cref="MercuryTextCodec.FromCharmapJson"/> path (text extraction only; no script execution).
    /// </summary>
    public static async Task DownloadCharmapAsync(IWin32Window? owner)
    {
        var baseData = _data ?? TryLoadDefaultProfileData();
        if (baseData is null)
        {
            WinFormsUtil.Error(L("NeedRom", "请先设置水银 ROM/profile，然后再导入字符映射。"));
            return;
        }

        var confirm = WinFormsUtil.Prompt(MessageBoxButtons.OKCancel,
            L("DownloadConfirm1", "将联网下载公开的 HOME 字表（含 charmap）并缓存到本机："),
            HomeCharmapUrl,
            L("DownloadConfirm2", "来源为用户可公开访问的研究页面，程序只解析其文本，不执行其中的脚本。是否继续？"));
        if (confirm != DialogResult.OK)
            return;

        try
        {
            using var client = MercuryDataSetup.CreateClient();
            var (text, codec) = await MercuryDataSetup.DownloadCharmapAsync(client);
            var updated = baseData.WithTextCodec(codec);
            CacheCharmap(text);
            Publish(updated);
            WinFormsUtil.Alert(L("DownloadDone", "字表下载并导入完成。"));
        }
        catch (Exception e)
        {
            WinFormsUtil.Error(L("DownloadFail", "下载字表失败（可改用本地导入）："), e);
        }
    }

    private static void CacheCharmap(string text) => MercuryDataSetup.CacheCharmap(ProfileDirectory, text);

    /// <summary>
    /// Imports an existing ROM-native research directory through the Core API.
    /// </summary>
    public static void ImportResearch(IWin32Window? owner)
    {
        using var fbd = new FolderBrowserDialog { Description = L("SelectResearch", "Select a Mercury research directory") };
        if (fbd.ShowDialog(owner) != DialogResult.OK)
            return;

        try
        {
            var data = MercuryGameData.FromResearch(fbd.SelectedPath);
            data.SaveProfile(ProfileDirectory);
            Publish(data);
            WinFormsUtil.Alert(L("ResearchImported", "研究目录已导入。"), $"种类 {data.Species.Count} / 招式 {data.Moves.Count} / 道具 {data.Items.Count}");
        }
        catch (Exception e)
        {
            WinFormsUtil.Error(L("ResearchInvalid", "研究目录导入失败。"), e);
        }
    }

    /// <summary>
    /// Loads an existing profile folder (containing mercury-profile.json) and adopts it as the active profile.
    /// </summary>
    public static void LoadProfileFolder(IWin32Window? owner)
    {
        using var fbd = new FolderBrowserDialog { Description = L("SelectProfile", "Select a Mercury profile folder") };
        if (fbd.ShowDialog(owner) != DialogResult.OK)
            return;

        try
        {
            var data = MercuryGameData.LoadProfile(fbd.SelectedPath);
            data.SaveProfile(ProfileDirectory);
            Publish(data);
            WinFormsUtil.Alert(L("ProfileLoaded", "水银配置已加载。"), $"{fbd.SelectedPath}");
        }
        catch (Exception e)
        {
            WinFormsUtil.Error(L("ProfileInvalid", "配置文件夹无效。"), e);
        }
    }

    private static MercuryGameData? TryLoadDefaultProfileData()
    {
        try
        {
            return Directory.Exists(ProfileDirectory) ? MercuryGameData.LoadProfile(ProfileDirectory) : null;
        }
        catch
        {
            return null;
        }
    }

    private static MercuryTextCodec? ExistingCodec()
    {
        try
        {
            if (File.Exists(CharmapPath))
                return MercuryDataSetup.ParseCharmap(File.ReadAllText(CharmapPath));
        }
        catch
        {
            // Fall through to the profile's own codec.
        }

        try
        {
            var codec = (_data ?? TryLoadDefaultProfileData())?.Text;
            return codec is { IsImported: true } ? codec : null;
        }
        catch
        {
            return null;
        }
    }

    // --------------------------------------------------------------- entity files

    private static readonly string[] MercuryExtensions = ["mercurypkm", "m3box", "m3pk", "m3party", "m3stored"];

    /// <summary>
    /// Reads Mercury single-entity files (58-byte boxed / 100-byte party records) by explicit extension and
    /// length. Never claims another format, so retail formats keep the built-in parser.
    /// </summary>
    private static PKM? TryReadEntity(Memory<byte> data, string ext, ITrainerInfo? sav)
    {
        if (!IsMercuryExtension(ext))
            return null;

        var gameData = (sav as MercurySaveFile)?.GameData ?? _data;
        if (gameData is null)
            return null;

        int length = data.Length;
        try
        {
            if (length == MercuryPokemon.BoxSize)
                return new MercuryPKM(gameData, MercuryPokemon.FromBox(data.ToArray()));
            if (length == MercuryPokemon.PartySize)
                return new MercuryPKM(gameData, MercuryPokemon.FromParty(data.ToArray()));
        }
        catch
        {
            return null;
        }
        return null;
    }

    private static bool IsMercuryExtension(string ext)
    {
        var span = ext.AsSpan().TrimStart('.');
        foreach (var candidate in MercuryExtensions)
        {
            if (span.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ sources

    private static readonly ConditionalWeakTable<MercuryGameData, Bitmap?[]> TypeImages = new();

    public static Bitmap? GetTypeImage(MercuryGameData data, byte type)
    {
        var images = TypeImages.GetValue(data, _ => new Bitmap?[256]);
        if (images[type] is { } existing)
            return existing;
        var rgba = data.GetTypeSpriteRgba(type, out int width, out int height);
        if (rgba is null)
            return null;
        for (int i = 0; i < rgba.Length; i += 4)
            (rgba[i], rgba[i + 2]) = (rgba[i + 2], rgba[i]);
        return images[type] = ImageUtil.GetBitmap(rgba, width, height, PixelFormat.Format32bppArgb);
    }

    private static readonly ConditionalWeakTable<MercuryGameData, Bitmap?[]> ItemImages = new();

    public static Bitmap? GetItemImage(MercuryGameData data, int item)
    {
        if ((uint)item >= (uint)data.Items.Count)
            return null; // outside the Mercury item table
        var images = ItemImages.GetValue(data, d => new Bitmap?[d.Items.Count]);
        if (images[item] is { } existing)
            return existing;
        var rgba = data.GetItemSpriteRgba(item, out int width, out int height);
        if (rgba is null)
            return null;
        for (int i = 0; i < rgba.Length; i += 4)
            (rgba[i], rgba[i + 2]) = (rgba[i + 2], rgba[i]);
        return images[item] = ImageUtil.GetBitmap(rgba, width, height, PixelFormat.Format32bppArgb);
    }

    private static Bitmap? GetSprite(PKM pk)
    {
        if (pk is not MercuryPKM mercury)
            return null;

        // Render from the entity's own data source so a stale global can never mix resources.
        var rgba = mercury.GameData.GetSpriteRgba(mercury.Species, mercury.PID, mercury.ID32, out int width, out int height);
        if (rgba is null)
            return null;

        // MercurySpriteLoader emits RGBA; GDI+ 32bppArgb memory order is BGRA.
        var bgra = new byte[rgba.Length];
        for (int i = 0; i + 3 < rgba.Length; i += 4)
        {
            bgra[i + 0] = rgba[i + 2];
            bgra[i + 1] = rgba[i + 1];
            bgra[i + 2] = rgba[i + 0];
            bgra[i + 3] = rgba[i + 3];
        }
        return ImageUtil.GetBitmap(bgra, width, height, PixelFormat.Format32bppArgb);
    }

    /// <summary>
    /// Produces a fresh format-specific <see cref="GameStrings"/> for Mercury saves from that save's own
    /// <see cref="MercurySaveFile.GameData"/>; returns null for every other save type so the retail cache is used.
    /// </summary>
    private sealed class MercuryGameStringsFactory
    {
        public GameStrings? Create(SaveFile sav, string lang)
        {
            if (sav is not MercurySaveFile mercury)
                return null;

            var data = mercury.GameData;
            var species = new string[data.Species.Count];
            for (int i = 0; i < species.Length; i++)
                species[i] = data.Species[i].Name;

            var moves = new string[data.Moves.Count];
            for (int i = 0; i < moves.Length; i++)
                moves[i] = data.Moves[i].Name;

            var items = new string[data.Items.Count];
            for (int i = 0; i < items.Length; i++)
                items[i] = data.Items[i].Name;

            // Native GameStrings sanitizes index 0 even when no ability names are loaded.
            var abilities = new string[Math.Max(1, data.AbilityNames.Count)];
            if (data.AbilityNames.Count == 0)
                abilities[0] = "0";
            for (int i = 0; i < data.AbilityNames.Count; i++)
                abilities[i] = data.AbilityNames[i];

            var strings = GameStrings.CreateMercury(lang, species, moves, items, abilities, data.GetBallNames(lang), data.GetLocationNames(lang));
            // This fresh instance is not the shared retail cache. Bind the complete four-bit ID space.
            data.GetOriginNames(lang).CopyTo(strings.gamelist, 0);
            return strings;
        }
    }
}
