using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Drawing;
using System.Windows.Forms;

namespace CheckLogic
{
    public enum CompareKind
    {
// ReSharper disable InconsistentNaming
        EQ,
        NE,
        LT,
        LE,
        GT,
        GE
// ReSharper restore InconsistentNaming
    }

    [Serializable]
    public class Comparator : Logic
    {
        public Comparator() { }

        private float Setpoint { get; set; }

        override public void SaveProperties(NameValueCollection coll)
        {
            base.SaveProperties(coll);
            var fp = CultureInfo.GetCultureInfo("en-US");
            coll["Kind"] = String.Format(fp, "Comparator.{0}", Kind);
            coll["Setpoint"] = String.Format(fp, "{0}", Setpoint);
        }

        private bool _loaded;

        public override void LoadProperties(NameValueCollection coll)
        {
            // далее загрузка свойств для типа Comparator
            var akind = (coll["Kind"] ?? "").Split(new[] { '.' });
            if (akind.Length != 2 || akind[0] != "Comparator") return;
            CompareKind kind;
            if (!Enum.TryParse(akind[1], out kind)) return;
            Kind = kind;
            var fp = CultureInfo.GetCultureInfo("en-US");
            base.LoadProperties(coll);
            float setpoint;
            if (float.TryParse(coll["Setpoint"] ?? "", NumberStyles.Float, fp, out setpoint))
                Setpoint = setpoint;
            _loaded = true;
        }

        public override void Calculate()
        {
            if (!_loaded || Outputs.Length != 1) return;
            var value = (float) (Inputs[1].Value ?? 0f);
            const float eps = 0.0001f;
            switch (_akind)
            {
                case CompareKind.EQ:
                    Outputs[1].Value = (Math.Abs(value - Setpoint) < eps) ^ Outputs[1].Invert;
                    break;
                case CompareKind.NE:
                    Outputs[1].Value = (Math.Abs(value - Setpoint) > eps) ^ Outputs[1].Invert;
                    break;
                case CompareKind.LT:
                    Outputs[1].Value = (value < Setpoint) ^ Outputs[1].Invert;
                    break;
                case CompareKind.LE:
                    Outputs[1].Value = (value <= Setpoint) ^ Outputs[1].Invert;
                    break;
                case CompareKind.GT:
                    Outputs[1].Value = (value > Setpoint) ^ Outputs[1].Invert;
                    break;
                case CompareKind.GE:
                    Outputs[1].Value = (value >= Setpoint) ^ Outputs[1].Invert;
                    break;
            }
            if (Outputs[1].Link != null)
                Outputs[1].Link.TransferLink(Outputs[1].Value);        
        }

        private CompareKind _akind;

        private CompareKind Kind
        {
            get { return _akind; }
            set
            {
                _akind = value;
                InpCount = 1;
                OutCount = 1;
                CalcDrawingSize();
                Inputs[1].Value = 0f;
                Outputs[1].Value = false;
            }
        }

        // реализация интерфейса ICloneable
        override public object Clone()
        {
            var plot = new Comparator(Plots, _akind) {Setpoint = Setpoint};
            return plot;
        }

        public Comparator(PlotsOwner owner, CompareKind kind)
            : this(owner)
        {
            Kind = kind;
        }

        public Comparator(PlotsOwner owner)
            : base(owner)
        {
            CalcDrawingSize();
        }

        override protected string FuncName()
        {
            var funcName = "(undef)";
            switch (_akind)
            {
                case CompareKind.EQ:
                    funcName = "==";
                    break;
                case CompareKind.NE:
                    funcName = "<>";
                    break;
                case CompareKind.LT:
                    funcName = "<";
                    break;
                case CompareKind.LE:
                    funcName = "<=";
                    break;
                case CompareKind.GT:
                    funcName = ">";
                    break;
                case CompareKind.GE:
                    funcName = ">=";
                    break;
            }
            return funcName;
        }

        override protected void AddPopupItems(ContextMenuStrip popup, HitInfo hitinfo)
        {
            switch (hitinfo.Hits)
            {
                case PlotHits.Body:
                    base.AddPopupItems(popup, hitinfo);
                    if (Plots.Emulation) break;
                    popup.Items.Add(new ToolStripSeparator());
                    var item = new ToolStripMenuItem("Изменить величину уставки...");
                    item.Click += (sender, args) =>
                    {
                        var frm = new InputValueForm {FloatValue = Setpoint};
                        if (frm.ShowDialog() != DialogResult.OK) return;
                        Setpoint = frm.FloatValue;
                        Refresh(true);
                    };
                    popup.Items.Add(item);
                    break;
            }
        }

        override protected bool InvertEnabled(PlotHits hits, uint inOut)
        {
            // для входов запрещено
            return hits == PlotHits.OutputLink;
        }

        public override void DrawAt(Graphics g)
        {
            base.DrawAt(g);
            var rect = Bounds;
            if (rect.IsEmpty) return;
            var timerect = new RectangleF(rect.Location, new SizeF(rect.Width, Height));
            timerect.Offset(0, Height/2);
            var fp = CultureInfo.GetCultureInfo("en-US");
            var value = String.Format(fp, "{0}", Setpoint);
            using (var font = new Font("Arial", 8f))
            {
                {
                    g.DrawString(value, SystemFonts.MenuFont, SystemBrushes.WindowText, timerect,
                                 new StringFormat
                                     {
                                         Alignment = StringAlignment.Center,
                                         LineAlignment = StringAlignment.Center
                                     });
                }
            }
        }
    }
}
