using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace CheckLogic
{
    public enum EmulateKind
    {
        Latch,
        Valve,
        Pump
    }

    [Serializable]
    public class Emulate : Logic
    {
        public Emulate() { }

        override public void SaveProperties(NameValueCollection coll)
        {
            base.SaveProperties(coll);
            var fp = CultureInfo.GetCultureInfo("en-US");
            coll["Kind"] = String.Format(fp, "Emulate.{0}", Kind);
            coll["Seconds"] = String.Format(fp, "{0}", Seconds);
            coll["Mode"] = String.Format(fp, "{0}", Mode);
        }

        private bool _loaded;

        public override void LoadProperties(NameValueCollection coll)
        {
            // далее загрузка свойств для типа Emulate
            var akind = (coll["Kind"] ?? "").Split(new[] { '.' });
            if (akind.Length != 2 || akind[0] != "Emulate") return;
            EmulateKind kind;
            if (!Enum.TryParse(akind[1], out kind)) return;
            Kind = kind;
            base.LoadProperties(coll);
            float seconds;
            if (float.TryParse((coll["Seconds"] ?? "").Replace(',', '.'), 
                NumberStyles.Any, CultureInfo.GetCultureInfo("en-US"), out seconds))
                Seconds = seconds;
            uint mode;
            if (uint.TryParse(coll["Mode"] ?? "", out mode))
                Mode = mode;
            _loaded = true;
        }

        private float _seconds = 5f;
        private int _second;

        private bool _m15, _m17;
        private float _m13;

        private float Seconds
        {
            get { return _seconds; }
            set { _seconds = value; }
        }

        public uint Mode { get; set; }

        public override void Calculate()
        {
            if (!_loaded) return;
            for (uint i = 1; i <= InpCount; i++)
                if (Inputs[i].Value == null) Inputs[i].Value = false;
            var dtm = DateTime.Now;
            var tick = false;
            if (_second != dtm.Second)
            {
                _second = dtm.Second;
                tick = true;
            }
            switch (_akind)
            {
                case EmulateKind.Latch:
                    if (Mode == 0) break; // режим не выбран
                    var open = Mode == 1 ? _localOpen : (bool)(Inputs[1].Value ?? false);
                    var close = Mode == 1 ? _localClose : (bool)(Inputs[2].Value ?? false);
                    var stop = Mode == 1 ? _localStop : (bool)(Inputs[3].Value ?? false);
                    var m6 = open && !close;
                    var m7 = !open && close;
                    var m8 = tick && m6 && !_m15;
                    var m9 = tick && m7 && !_m17;
                    var m10 = m8 || m9;
                    var m11 = m7 ? -1f : 1f;
                    var m14 = m10 && !stop;
                    var m12 = m14 ? m11 : 0f;
                    _m13 = _m13 + m12;
                    _m15 = _m13 > _seconds;
                    _m17 = _m13 <= 0f;
                    Outputs[1].Value = _m15 || _localCross;
                    Outputs[2].Value = _m17 || _localCross;
                    Outputs[3].Value = open && close || _localError;
                    if (Outputs[1].Link != null)
                        Outputs[1].Link.TransferLink(Outputs[1].Value);
                    if (Outputs[2].Link != null)
                        Outputs[2].Link.TransferLink(Outputs[2].Value);
                    if (Outputs[3].Link != null)
                        Outputs[3].Link.TransferLink(Outputs[3].Value);
                    break;
            }
        }

        private EmulateKind _akind;
        private bool _localOpen;
        private bool _localClose;
        private bool _localStop;
        private bool _localError;
        private bool _localCross;

        private EmulateKind Kind
        {
            get { return _akind; }
            set
            {
                _akind = value;
                switch (_akind)
                {
                    case EmulateKind.Latch:
                        InpCount = 3;
                        Inputs[1].Value = false;
                        Inputs[1].Name = "OP";
                        Inputs[2].Value = false;
                        Inputs[2].Name = "CL";
                        Inputs[3].Value = false;
                        Inputs[3].Name = "ST";
                        OutCount = 3;
                        Outputs[1].Value = false;
                        Outputs[1].Name = "OPN";
                        Outputs[2].Value = true;
                        Outputs[2].Name = "CLO";
                        Outputs[3].Value = false;
                        Outputs[3].Name = "ERR";
                        CalcDrawingSize();
                        break;
                }
            }
        }


        // реализация интерфейса ICloneable
        override public object Clone()
        {
            var plot = new Emulate(Plots, _akind) { Seconds = Seconds, Mode = Mode };
            return plot;
        }

        public Emulate(PlotsOwner owner, EmulateKind kind)
            : this(owner)
        {
            Kind = kind;
        }

        public Emulate(PlotsOwner owner)
            : base(owner)
        {
            CalcDrawingSize();
        }

        override protected string FuncName()
        {
            var funcName = "(undef)";
            switch (_akind)
            {
                case EmulateKind.Latch:
                    funcName = "LATCH";
                    break;
                case EmulateKind.Valve:
                    funcName = "VALVE";
                    break;
                case EmulateKind.Pump:
                    funcName = "PUMP";
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
                    if (_akind == EmulateKind.Latch)
                    {
                        ToolStripMenuItem item;
                        if (!Plots.Emulation)
                        {
                            popup.Items.Add(new ToolStripSeparator());
                            item = new ToolStripMenuItem("Изменить время хода...");
                            item.Click += (sender, args) =>
                                {
                                    var frm = new InputValueForm {FloatValue = Seconds};
                                    if (frm.ShowDialog() != DialogResult.OK) return;
                                    if (!(frm.FloatValue >= 1f)) return;
                                    Seconds = frm.FloatValue;
                                    Refresh(true);
                                };
                            popup.Items.Add(item);
                            popup.Items.Add(new ToolStripSeparator());
                        }
                        var submenu = new ToolStripMenuItem("Выбор режима");
                        popup.Items.Add(submenu);
                        item = new ToolStripMenuItem("1 - Местный режим") { Checked = Mode == 1 };
                        item.Click += (sender, args) =>
                            {
                                Mode = 1;
                                Refresh(!Plots.Emulation);
                            };
                        submenu.DropDownItems.Add(item);
                        item = new ToolStripMenuItem("0 - нет выбора") {Checked = Mode == 0};
                        item.Click += (sender, args) =>
                        {
                            Mode = 0;
                            Refresh(!Plots.Emulation);
                        };
                        submenu.DropDownItems.Add(item);
                        item = new ToolStripMenuItem("2 - Дистанционный режим") {Checked = Mode == 2};
                        item.Click += (sender, args) =>
                        {
                            Mode = 2;
                            Refresh(!Plots.Emulation);
                        };
                        submenu.DropDownItems.Add(item);
                        if (Plots.Emulation && Mode == 1) // локальный режим
                        {
                            popup.Items.Add(new ToolStripSeparator());
                            submenu = new ToolStripMenuItem("Местное управление");
                            popup.Items.Add(submenu);
                            item = new ToolStripMenuItem("Открыть задвижку") {Checked = _localOpen};
                            item.Click += (sender, args) =>
                                {
                                    _localOpen = true;
                                    _localClose = false;
                                    _localStop = false;
                                };
                            submenu.DropDownItems.Add(item);
                            item = new ToolStripMenuItem("Закрыть задвижку") {Checked = _localClose};
                            item.Click += (sender, args) =>
                            {
                                _localOpen = false;
                                _localClose = true;
                                _localStop = false;
                            };
                            submenu.DropDownItems.Add(item);
                            item = new ToolStripMenuItem("Остановить задвижку") {Checked = _localStop};
                            item.Click += (sender, args) =>
                            {
                                _localOpen = false;
                                _localClose = false;
                                _localStop = true;
                            };
                            submenu.DropDownItems.Add(item);
                        }
                        if (Plots.Emulation && Mode != 0) // имитация ошибок
                        {
                            popup.Items.Add(new ToolStripSeparator());
                            item = new ToolStripMenuItem("Внутренняя авария задвижки") {Checked = _localError};
                            item.Click += (sender, args) =>
                            {
                                _localError = !_localError;
                            };
                            popup.Items.Add(item);
                            item = new ToolStripMenuItem("Пересечение концевиков") {Checked = _localCross};
                            item.Click += (sender, args) =>
                            {
                                _localCross = !_localCross;
                            };
                            popup.Items.Add(item);
                        }
                    }
                    break;
            }
        }

        public override void DrawAt(Graphics g)
        {
            base.DrawAt(g);
            var rect = Bounds;
            if (rect.IsEmpty) return;
            if (_akind == EmulateKind.Latch)
            {
                var timerect = new RectangleF(rect.Location, new SizeF(rect.Width, Height));
                timerect.Offset(0, Height);
                var opened = (bool) Outputs[1].Value;
                var closed = (bool) Outputs[2].Value;
                var errored = (bool) Outputs[3].Value;
                var stop = Mode != 0 && (Mode == 1 ? _localStop : (bool) Inputs[3].Value);
                var value = Plots.Emulation 
                    ? String.Format("{0}", errored ? "АВАРИЯ" : stop ? "СТОП" : opened ? "ОТКРЫТ" : closed ? "ЗАКРЫТ" : "ХОД")
                    : String.Format("Ход {0} с", Seconds);
                using (var font = new Font("Arial Narrow", 8f))
                {
                    g.DrawString(value, font, SystemBrushes.WindowText, timerect,
                                 new StringFormat
                                     {
                                         Alignment = StringAlignment.Center,
                                         LineAlignment = StringAlignment.Center
                                     });
                }
                timerect.Offset(0, Height);
                var mode = Mode == 0 ? "Не выбр." : Mode == 1 ? "Местный" : "Дистанц.";
                using (var font = new Font("Arial Narrow", 8f))
                {
                    g.DrawString(mode, font, SystemBrushes.WindowText, timerect,
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
