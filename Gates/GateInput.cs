using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Drawing;
using System.Windows.Forms;

namespace CheckLogic
{
    public enum GateInputKind
    {
        // ReSharper disable InconsistentNaming
        Fixed,
        DI,
        Impulse,
        Reset,
        AI
        // ReSharper restore InconsistentNaming
    }

    [Serializable]
    public class GateInput : Logic
    {
        public GateInput() { }

        public readonly List<string> Lines = new List<string>();

        override public void SaveProperties(NameValueCollection coll)
        {
            base.SaveProperties(coll);
            var fp = CultureInfo.GetCultureInfo("en-US");
            coll["Kind"] = String.Format(fp, "GateInput.{0}", Kind);
            var n = 1;
            foreach (var line in Lines)
                coll["Line" + (n++)] = line;
        }

        private bool _loaded;

        public override void LoadProperties(NameValueCollection coll)
        {
            // далее загрузка свойств для типа GateInput
            var akind = (coll["Kind"] ?? "").Split(new[] { '.' });
            if (akind.Length != 2 || akind[0] != "GateInput") return;
            GateInputKind kind;
            if (!Enum.TryParse(akind[1], out kind)) return;
            Kind = kind;
            base.LoadProperties(coll);
            var aOutputs = (coll["Outputs"] ?? "").Split(new[] { ',' });
            var fp = CultureInfo.GetCultureInfo("en-US");
            for (uint i = 0; i < aOutputs.Length; i++)
                if (i < Outputs.Length)
                {
                    bool b;
                    if (bool.TryParse(aOutputs[i], out b))
                        Outputs[i + 1].Value = b;
                    float value;
                    if (float.TryParse(aOutputs[i], NumberStyles.Float, fp, out value))
                        Outputs[i + 1].Value = value;
                }
            _value = Outputs[1].Value;
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
            _loaded = true;
        }    

        private GateInputKind _akind;

        private GateInputKind Kind
        {
            get { return _akind; }
            set
            {
                _akind = value;
                switch (_akind)
                {
                    case GateInputKind.Fixed:
                        if (Lines.Count == 0)
                            Lines.Add("Входной переключаемый сигнал");
                        break;
                    case GateInputKind.Impulse:
                        if (Lines.Count == 0)
                            Lines.Add("Входной импульсный сигнал");
                        break;
                    case GateInputKind.Reset:
                        if (Lines.Count == 0)
                            Lines.Add("Сброс неисправности");
                        break;
                    case GateInputKind.DI:
                        if (Lines.Count == 0)
                            Lines.Add("Входной дискретный сигнал");
                        break;
                    case GateInputKind.AI:
                        if (Lines.Count == 0)
                            Lines.Add("Входной аналоговый сигнал");
                        break;
                }
                InpCount = 0;
                OutCount = 1;
                CalcDrawingSize();
            }
        }

        // реализация интерфейса ICloneable
        override public object Clone()
        {
            var plot = new GateInput(Plots, _akind);
            return plot;
        }

        override public uint InpCount // количество входов
        {
            get { return 0; }
            protected set { base.InpCount = value; }
        }

        public GateInput(PlotsOwner owner, GateInputKind kind)
            : this(owner)
        {
            Kind = kind;
            switch (kind)
            {
                case GateInputKind.AI:
                    Outputs[1].Value = 0f;
                    break;
                default:
                    Outputs[1].Value = false;
                    break;
            }
        }

        public GateInput(PlotsOwner owner)
            : base(owner)
        {
            CalcDrawingSize();
        }

        override protected string FuncName()
        {
            var funcName = "(undef)";
            switch (_akind)
            {
                case GateInputKind.Fixed:
                    funcName = "F фикс.";
                    break;
                case GateInputKind.DI:
                    funcName = "DI";
                    break;
                case GateInputKind.Impulse:
                    funcName = "F имп.";
                    break;
                case GateInputKind.Reset:
                    funcName = "F сбр.";
                    break;
                case GateInputKind.AI:
                    funcName = "AI";
                    break;
            }
            return funcName;
        }

        override public void DrawAt(Graphics g) // рисование элемента
        {
            var rect = base.Bounds;
            if (rect.IsEmpty) return;
            rect.Offset(-BaseSize * 3, 0);
            rect.Width += BaseSize * 2;
            g.FillRectangles(Selected ? SystemBrushes.ControlLight : SystemBrushes.Window, new[] { rect });
            using (var pen = new Pen(_gateoutput == null ? SystemColors.WindowText : SystemColors.ControlDarkDark))
            {
                g.DrawRectangles(pen, new[] {rect});
            }            
            base.DrawAt(g);
            var text = String.Join("\r\n", Lines);
            using (var font = new Font("Arial", 9f))
            {
                g.DrawString(text, font,
                             _gateoutput == null ? SystemBrushes.WindowText : SystemBrushes.ControlDarkDark, rect,
                             new StringFormat
                                 {
                                     Alignment = StringAlignment.Center,
                                     LineAlignment = StringAlignment.Center
                                 });
            }
        }

        private object _value;
        private bool _strobOn;
        private bool _strobOff;
        private GateOutput _gateoutput;

        override public void Calculate()
        {
            if (!_loaded || Outputs.Length != 1) return;
            switch (_akind)
            {
                case GateInputKind.Fixed:
                case GateInputKind.DI:
                    Outputs[1].Value = (bool)(_value ?? false) ^ Outputs[1].Invert;
                    if (Outputs[1].Link != null)
                        Outputs[1].Link.TransferLink(Outputs[1].Value);
                    break;
                case GateInputKind.Impulse:
                case GateInputKind.Reset:
                    if (_strobOn && !_strobOff)
                    {
                        _strobOn = false;
                        _strobOff = true;
                        if (Outputs[1].Link != null)
                            Outputs[1].Link.TransferLink(true);
                    }
                    else if (!_strobOn && _strobOff)
                    {
                        _strobOff = false;
                        if (Outputs[1].Link != null)
                            Outputs[1].Link.TransferLink(false);
                    }
                    break;
                case GateInputKind.AI:
                    Outputs[1].Value = _value;
                    if (Outputs[1].Link != null)
                        Outputs[1].Link.TransferLink(Outputs[1].Value);
                    break;
            }
        }

        override public bool ClickAt(PointF point)
        {
            var info = CheckMouseHitAt(point);
            if (info.Hits == PlotHits.Descriptor) return false;
            if (Outputs.Length != 1) return false;
            Action warning = () => MessageBox.Show(
                String.Format(
                    @"Этот вход связан с выходом Лист {0}, L{1}.1 {2}{3}и напрямую изменить его значение нельзя.",
                    _gateoutput.PageNum,
                    _gateoutput.OrderNum,
                    (_gateoutput.Name != null ? "{" + _gateoutput.Name + "} " : ""),
                    Environment.NewLine),
                @"Изменение значения", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            switch (_akind)
            {
                case GateInputKind.Fixed:
                case GateInputKind.DI:
                    if (_gateoutput == null)
                    {
                        _value = !(bool) (Outputs[1].Value ?? false);
                        Outputs[1].Value = _value;
                        return true;
                    }
                    warning();
                    break;
                case GateInputKind.Impulse:
                case GateInputKind.Reset:
                    _strobOn = true;
                    return true;
                case GateInputKind.AI:
                    if (_gateoutput == null)
                    {
                        var frm = new InputValueForm {FloatValue = (float) (Outputs[1].Value ?? 0f)};
                        if (frm.ShowDialog() == DialogResult.OK)
                        {
                            _value = frm.FloatValue;
                            Outputs[1].Value = _value;
                            return true;
                        }
                    }
                    else
                        warning();
                    break;
            }
            return false;
        }

        override public RectangleF BoundsRect
        {
            get
            {
                var rect = Bounds;
                rect.Offset(-BaseSize * 3, 0);
                rect.Width += BaseSize * 3;
                return rect;
            }
        }

        override public HitInfo CheckMouseHitAt(PointF point)
        {
            var rect = Bounds;
            if (rect.IsEmpty)
                return new HitInfo { Hits = PlotHits.None };
            // выход
            rect.Offset(-BaseSize * 3, 0);
            rect.Width += BaseSize * 2;
            // выходы
            var pt1 = new PointF(rect.Location.X + rect.Width, rect.Location.Y + Height);
            var pt2 = new PointF(rect.Location.X + rect.Width + PinSize,
                             rect.Location.Y + Height);
            for (var i = 1; i <= OutCount; i++)
            {
                // места для щелчка мышки
                var rp = new RectangleF(pt1, new SizeF(PinSize, Height));
                rp.Offset(BaseSize, -Height * 0.9f);
                if (rp.Contains(point) && HitEnabled(PlotHits.OutputLink, (uint)i))
                    return new HitInfo { Hits = PlotHits.OutputLink, InOut = (uint)i };
                // смещение
                pt1.Y += Height;
                pt2.Y += Height;
            }
            // дескриптор
            if (rect.Contains(point) && HitEnabled(PlotHits.Descriptor))
                return new HitInfo { Hits = PlotHits.Descriptor };
            return base.CheckMouseHitAt(point);
        }

        override protected bool InvertEnabled(PlotHits hits, uint inOut)
        {
            // для выходов запрещено
            return false;
        }

        public void SetGateOutput(GateOutput gateoutput)
        {
            if (gateoutput == null) return;
            _gateoutput = gateoutput;
            _gateoutput.AddGateInput(this);
        }

        public void RemoveGateOutput()
        {
            if (_gateoutput == null) return;
            _gateoutput.RemoveGateOutput(this);
            _gateoutput = null;
        }

        public bool ExternalLinked()
        {
            return _gateoutput != null;
        }

        public void TransferGateValue(object value)
        {
            _value = value;
        }

        override protected void AddPopupItems(ContextMenuStrip popup, HitInfo hitinfo)
        {
            var kindes = new[] {GateInputKind.DI, GateInputKind.AI};
            if (!kindes.Contains(_akind)) return;
            switch (hitinfo.Hits)
            {
                case PlotHits.Body:
                    base.AddPopupItems(popup, hitinfo);
                    if (Plots.Emulation) break;
                    popup.Items.Add(new ToolStripSeparator());
                    if (_gateoutput == null)
                    {
                        var item = new ToolStripMenuItem("Привязать выход к этому входу...");
                        PopupMenu.Items.Add(item);
                        item.Click += (sender, args) =>
                            {
                                var list = new List<GateOutput>();
                                switch (_akind)
                                {
                                    case GateInputKind.DI:
                                        list.AddRange(Plots.GetAllPlots().OfType<GateOutput>()
                                                           .Where(
                                                               plt =>
                                                               plt.Kind == GateOutputKind.DO));
                                        break;
                                    case GateInputKind.AI:
                                        list.AddRange(Plots.GetAllPlots().OfType<GateOutput>()
                                                           .Where(
                                                               plt =>
                                                               plt.Kind == GateOutputKind.AO));
                                        break;
                                }
                                var frm = new InputLinkForm(list, _gateoutput);
                                if (frm.ShowDialog() != DialogResult.OK) return;
                                if (frm.Selected == null) return;
                                var output = frm.Selected;
                                RemoveGateOutput();
                                SetGateOutput(output);
                                Refresh(true);
                            };
                    }
                    else
                    {
                        if (_gateoutput != null)
                        {
                            var item = new ToolStripMenuItem(
                                String.Format(
                                "Удалить привязку в выходу Лист {0}, L{1}.1{2}", 
                                _gateoutput.PageNum, 
                                _gateoutput.OrderNum,
                                _gateoutput.Name != null ? " {"+_gateoutput.Name+"}" : "")
                                );
                            PopupMenu.Items.Add(item);
                            item.Click += (sender, args) =>
                                {
                                    RemoveGateOutput();
                                    Refresh(true);
                                };
                        }
                    }
                    break;
            }
        }
    }
}
