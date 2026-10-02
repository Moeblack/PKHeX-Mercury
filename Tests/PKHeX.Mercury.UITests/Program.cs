using System.Reflection;
using System.Text.Json;
using PKHeX.Mercury.Core;

internal static class Program
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly List<string> Checks = [];

    [STAThread]
    private static int Main(string[] args)
    {
        string output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/ui-checks");
        Directory.CreateDirectory(output);
        Form? form = null;
        object result;
        int exit;
        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var ui = Assembly.Load("PKHeX.Mercury");
            var state = ui.GetType("PKHeX.Mercury.AppState", true)!;
            state.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
            var fixture = Assembly.Load("PKHeX.Mercury.Tests").GetType("PKHeX.Mercury.Tests.Program", true)!
                .GetMethod("Fixture", BindingFlags.Static | BindingFlags.NonPublic)!;
            var initial = (byte[])fixture.Invoke(null, [8u, 9u, true])!;
            var seed = MercurySave.Load(initial);
            var mon = MercuryPokemon.Create(25, 0x12345678);
            mon.NicknameBytes = MercuryTextCodec.Default().Encode("AB", 10);
            mon.OTNameBytes = MercuryTextCodec.Default().Encode("CD", 7);
            mon.PID = 12345;
            mon.Experience = 125000;
            mon.Friendship = 70;
            mon.Moves = [1, 0, 0, 0];
            seed.SetBox(0, 0, mon);
            string input = Path.Combine(output, "synthetic-input.srm");
            File.WriteAllBytes(input, seed.Export());
            byte[] inputSnapshot = File.ReadAllBytes(input);

            form = (Form)Activator.CreateInstance(ui.GetType("PKHeX.Mercury.MainForm", true)!, true)!;
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new System.Drawing.Point(-20000, -20000);
            form.Show();
            Application.DoEvents();
            Check(form.IsHandleCreated, "real main-window handle created");
            Call(form, "LoadSave", input);
            Application.DoEvents();
            var loaded = Field<MercurySave>(form, "_save");
            Check(loaded.GetBox(0, 0).Species == 25, "UI open loads selected synthetic file");
            Check(Field<ListBox>(form, "_boxList").Items.Count == 25, "25 boxes populated");
            Check(Descendants(form).Count(c => c.GetType().Name == "SlotControl") == 36, "30 box and 6 party controls created");

            var slotType = ui.GetType("PKHeX.Mercury.SlotRef", true)!;
            object Slot(int box, int index) => Activator.CreateInstance(slotType, [false, box, index])!;
            Call(form, "SelectSlot", Slot(0, 0));
            Application.DoEvents();
            var basic = Descendants(form).Single(c => c.GetType().Name == "BasicTab");
            var friendship = Field<NumericUpDown>(basic, "_numFriend");
            friendship.Value = 71;
            Application.DoEvents();
            Check(Field<bool>(form, "_draftDirty"), "control event marks draft dirty");
            Check(loaded.GetBox(0, 0).Friendship == 70, "draft does not mutate save before Apply");
            Call(form, "ApplyDraft");
            Check(loaded.GetBox(0, 0).Friendship == 71, "Apply commits control edit to selected slot");
            Check(!Field<bool>(form, "_draftDirty"), "Apply clears draft dirty state");

            Call(form, "CopySlot", Slot(0, 0));
            Call(form, "PasteSlot", Slot(24, 29));
            Check(loaded.GetBox(24, 29).Species == 25 && loaded.GetBox(24, 29).Friendship == 71, "UI copy/paste reaches box25 slot30");
            Call(form, "CutSlot", Slot(0, 0));
            Call(form, "PasteSlot", Slot(0, 1));
            Check(loaded.GetBox(0, 0).IsEmpty && loaded.GetBox(0, 1).Species == 25, "UI cut/move clears source without overwriting adjacent slots");
            Call(form, "SelectSlot", Slot(0, 1));
            friendship.Value = 72;
            Call(form, "DiscardDraft");
            Check(loaded.GetBox(0, 1).Friendship == 71 && !Field<bool>(form, "_draftDirty"), "Discard restores draft and retains saved value");
            Check(((string)Call(form, "DefaultSaveName")!).EndsWith("_mercury.srm", StringComparison.OrdinalIgnoreCase), "Save As defaults to a new filename");
            string exported = Path.Combine(output, "synthetic-edited.srm");
            File.WriteAllBytes(exported, loaded.Export());
            var reopened = MercurySave.Load(File.ReadAllBytes(exported));
            Check(reopened.GetBox(24, 29).Species == 25 && reopened.GetBox(0, 1).Friendship == 71, "UI-edited state exports and reopens");
            Check(inputSnapshot.SequenceEqual(File.ReadAllBytes(input)), "UI scenario never changes source file");
            result = new { status = "passed", checks = Checks, scope = "WinForms construction and actual control/edit handlers using a synthetic save; no game/emulator or visual-aesthetic acceptance." };
            exit = 0;
        }
        catch (Exception ex)
        {
            while (ex is TargetInvocationException { InnerException: not null } tie) ex = tie.InnerException!;
            result = new { status = "failed", checks = Checks, error = ex.ToString() };
            exit = 1;
        }
        finally { form?.Dispose(); }
        string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
        File.WriteAllText(Path.Combine(output, "verification-ui.json"), json);
        return exit;
    }

    private static object? Call(object instance, string method, params object?[] args) =>
        instance.GetType().GetMethod(method, InstanceFlags)!.Invoke(instance, args);
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, InstanceFlags)!.GetValue(instance)!;
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control grandchild in Descendants(child)) yield return grandchild;
        }
    }
    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException(name);
        Checks.Add(name);
    }
}
