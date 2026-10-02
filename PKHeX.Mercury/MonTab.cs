using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

/// <summary>详情页签基类：直接编辑草稿副本，变更通过 Modified 通知主窗体。</summary>
internal abstract class MonTab : UserControl
{
    public event Action? Modified;

    protected MercuryPokemon? Mon;
    protected MercurySpecies? Species;
    protected bool Loading;

    public void Bind(MercuryPokemon? mon)
    {
        Mon = mon;
        Species = mon is null || mon.IsEmpty ? null : SafeSpecies(mon.Species);
        Reload();
    }

    protected abstract void Reload();

    protected void Mark()
    {
        if (!Loading)
            Modified?.Invoke();
    }

    protected static MercurySpecies? SafeSpecies(int id)
    {
        try
        {
            return AppState.Data.GetSpecies(id);
        }
        catch
        {
            return null;
        }
    }

    protected static byte[] EnsureLength(byte[]? source, int length)
    {
        var result = new byte[length];
        if (source is not null)
            Array.Copy(source, result, Math.Min(source.Length, length));
        return result;
    }
}
