using System;
using System.Windows.Forms;
using PKHeX.Core;
using PKHeX.Drawing.Misc;
using PKHeX.Mercury.Core;

namespace PKHeX.WinForms.Controls;

public partial class MoveChoice : UserControl
{
    private EntityContext Context;
    private MercuryGameData? MercuryData;

    public MoveChoice()
    {
        InitializeComponent();
        CB_Move.InitializeBinding();
    }

    public ushort SelectedMove { get => (ushort)WinFormsUtil.GetIndex(CB_Move); set => CB_Move.SelectedValue = (int)value; }
    public int PP { get => SelectedMove == 0 ? 0 : Util.ToInt32(TB_PP.Text); set => TB_PP.Text = value.ToString(); }
    public int PPUps { get => SelectedMove == 0 ? 0 : CB_PPUps.SelectedIndex; set => LoadClamp(CB_PPUps, value); }
    public bool HideLegality { private get; set; }
    public void SetContext(EntityContext context) => Context = context;

    /// <summary>
    /// Sets the format-specific move table (Mercury internal ids) used for the type icon; null uses retail <see cref="MoveInfo"/>.
    /// </summary>
    public void SetMercuryMoveSource(MercuryGameData? data) => MercuryData = data;

    private void UpdateTypeSprite(int value)
    {
        if (value <= 0)
        {
            PB_Type.Image = null;
            return;
        }

        if (MercuryData is { } md)
        {
            PB_Type.Image = (uint)value < (uint)md.Moves.Count
                ? MercuryIntegration.GetTypeImage(md, md.Moves[value].Type) : null;
            return;
        }
        byte type = MoveInfo.GetType((ushort)value, Context);
        PB_Type.Image = TypeSpriteUtil.GetTypeSpriteIconSmall(type);
    }

    private static void LoadClamp(ComboBox cb, int value)
    {
        var max = cb.Items.Count - 1;
        if (value > max)
            value = max;
        else if (value < -1)
            value = 0;
        cb.SelectedIndex = value;
    }

    public void UpdateLegality(MoveResult move, PKM entity, int i)
    {
        if (HideLegality)
        {
            PB_Triangle.Visible = false;
            return;
        }
        PB_Triangle.Visible = true;
        PB_Triangle.Image = MoveDisplayState.GetMoveImage(!move.Valid, entity, i);
    }

    /// <summary>
    /// Sets the move legality triangle for a format-specific (Mercury) move, without touching retail move tables.
    /// </summary>
    public void SetMercuryLegality(bool valid)
    {
        if (HideLegality)
        {
            PB_Triangle.Visible = false;
            return;
        }
        PB_Triangle.Visible = true;
        PB_Triangle.Image = valid ? null : PKHeX.Drawing.PokeSprite.SpriteUtil.GetLegalIndicator(false);
    }

    public void HealPP(PKM pk)
    {
        var move = SelectedMove;
        var up = PPUps;
        if (move == 0)
            PPUps = up = 0;
        PP = pk.GetMovePP(move, up);
    }

    private void CB_Move_SelectedIndexChanged(object sender, EventArgs e)
    {
        var value = WinFormsUtil.GetIndex(CB_Move);
        UpdateTypeSprite(value);
    }
}
