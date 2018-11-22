using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Drawing;
using System.Windows.Forms;

namespace CheckLogic
{
    public enum GateOutputKind
    {
        // ReSharper disable InconsistentNaming
        Lamp,
        Sound,
        DO,
        AO
        // ReSharper restore InconsistentNaming
    }

    [Serializable]
    public class GateOutput : Logic
    {
        public GateOutput() { }

        public readonly List<string> Lines = new List<string>();

        private readonly List<GateInput> _linkedInputs = new List<GateInput>();

        public void AddGateInput(GateInput gateinput)
        {
            if (_linkedInputs.Exists(inp => inp.Equals(gateinput))) return;
            _linkedInputs.Add(gateinput);
            gateinput.SetGateOutput(this);
        }

        public void RemoveGateOutput(GateInput gateinput)
        {
            _linkedInputs.RemoveAll(inp => inp.Equals(gateinput));
        }

        public void RemoveGateOutputs()
        {
            for (var i = _linkedInputs.Count - 1; i >= 0; i--)
                _linkedInputs[i].RemoveGateOutput();
            _linkedInputs.Clear();
        }

        override public void SaveProperties(NameValueCollection coll)
        {
            base.SaveProperties(coll);
            var fp = CultureInfo.GetCultureInfo("en-US");
            coll["Kind"] = String.Format(fp, "GateOutput.{0}", Kind);
            var n = 1;
            foreach (var line in Lines)
                coll["Line" + (n++)] = line;
            n = 1;
            foreach (var linkedInput in _linkedInputs)
            {
                coll["GateLink" + (n++)] =
                    String.Format(fp, "{0}.{1}.{2}", (PageNum != linkedInput.PageNum)
                                                         ? linkedInput.PageNum.ToString("0")
                                                         : "*",
                                  linkedInput.OrderNum,
                                  1);
            }
        }

        private bool _loaded;

        public override void LoadProperties(NameValueCollection coll)
        {
            // далее загрузка свойств для типа GateInput
            var akind = (coll["Kind"] ?? "").Split(new[] { '.' });
            if (akind.Length != 2 || akind[0] != "GateOutput") return;
            GateOutputKind kind;
            if (!Enum.TryParse(akind[1], out kind)) return;
            Kind = kind;
            base.LoadProperties(coll);
            Lines.Clear();
            var n = 1;
            while (true)
            {
                var line = coll["Line" + (n++)];
                if (line != null)
                    Lines.Add(line);
                else
                    break;
            }
            GateLinks.Clear();
            n = 1;
            while (true)
            {
                var line = coll["GateLink" + (n++)];
                if (line != null)
                    GateLinks.Add(line);
                else
                    break;
            }            
            _loaded = true;
        }

        private GateOutputKind _akind;

        public GateOutputKind Kind
        {
            get { return _akind; }
            private set
            {
                InpCount = 1;
                OutCount = 0;
                _akind = value;
                switch (_akind)
                {
                    case GateOutputKind.Lamp:
                         if (Lines.Count == 0)
                            Lines.Add("Лампа");
                        Inputs[1].Value = false;
                       break;
                    case GateOutputKind.Sound:
                       if (Lines.Count == 0)
                           Lines.Add("Звуковой сигнал");
                        Inputs[1].Value = false;
                       break;
                    case GateOutputKind.DO:
                       if (Lines.Count == 0)
                           Lines.Add("Дискретный выход");
                        Inputs[1].Value = false;
                       break;
                    case GateOutputKind.AO:
                       if (Lines.Count == 0)
                           Lines.Add("Аналоговый выход");
                        Inputs[1].Value = 0f;
                       break;
                }
                CalcDrawingSize();
            }
        }

        // реализация интерфейса ICloneable
        override public object Clone()
        {
            var plot = new GateOutput(Plots, _akind);
            return plot;
        }

        protected override uint OutCount // количество выходов
        {
            get { return 0; }
            set { base.OutCount = value; }
        }

        public GateOutput(PlotsOwner owner, GateOutputKind kind)
            : this(owner)
        {
            Kind = kind;
            switch (kind)
            {
                case GateOutputKind.AO:
                    Inputs[1].Value = 0f;
                    break;
                default:
                    Inputs[1].Value = false;
                    break;
            }
        }

        public GateOutput(PlotsOwner owner)
            : base(owner)
        {
            CalcDrawingSize();
        }

        public override void Calculate()
        {
            // заглушка
            if (!_loaded || Inputs.Length != 1) return;
            switch (_akind)
            {
                case GateOutputKind.Lamp:
                case GateOutputKind.Sound:
                    break;
                case GateOutputKind.DO:
                case GateOutputKind.AO:
                    foreach (var input in _linkedInputs)
                        input.TransferGateValue(Inputs[1].Value);
                    break;
            }
        }

        override protected
                    string FuncName()
        {
            var funcName = "(undef)";
            switch (_akind)
            {
                case GateOutputKind.Lamp:
                    funcName = "Лампа";
                    break;
                case GateOutputKind.Sound:
                    funcName = "Звук";
                    break;
                case GateOutputKind.DO:
                    funcName = "DO";
                    break;
                case GateOutputKind.AO:
                    funcName = "AO";
                    break;
            }
            return funcName;
        }

        override public void DrawAt(Graphics g) // рисование элемента
        {
            var rect = base.Bounds;
            if (rect.IsEmpty) return;
            rect.Offset(BaseSize, 0);
            rect.Width += BaseSize * 2;
            g.FillRectangles(Selected ? SystemBrushes.ControlLight : SystemBrushes.Window, new[] { rect });
            using (var pen = new Pen(_linkedInputs.Count == 0 ? SystemColors.WindowText : SystemColors.ControlDarkDark))
            {
                g.DrawRectangles(pen, new[] { rect });
            }
            base.DrawAt(g);
            var text = String.Join("\r\n", Lines);
            using (var font = new Font("Arial", 9f))
            {
                g.DrawString(text, font,
                             _linkedInputs.Count == 0 ? SystemBrushes.WindowText : SystemBrushes.ControlDarkDark, rect,
                             new StringFormat
                                 {
                                     Alignment = StringAlignment.Center,
                                     LineAlignment = StringAlignment.Center
                                 });
            }
        }

        override public RectangleF BoundsRect
        {
            get
            {
                var rect = Bounds;
                rect.Width += BaseSize * 3;
                return rect;
            }
        }

        override public HitInfo CheckMouseHitAt(PointF point)
        {
            var rect = Bounds;
            if (rect.IsEmpty)
                return new HitInfo { Hits = PlotHits.None };
            rect.Offset(BaseSize, 0);
            rect.Width += BaseSize * 2;
            // дескриптор
            if (rect.Contains(point) && HitEnabled(PlotHits.Descriptor))
                return new HitInfo { Hits = PlotHits.Descriptor };
            return base.CheckMouseHitAt(point);
        }

        override protected bool InvertEnabled(PlotHits hits, uint inOut)
        {
            // для входов запрещено
            return false;
        }

        override protected void AddPopupItems(ContextMenuStrip popup, HitInfo hitinfo)
        {
            // заглушка
        }
    }
}
