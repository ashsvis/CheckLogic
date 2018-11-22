using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Drawing;
using System.Windows.Forms;

namespace CheckLogic
{
    public enum TimerKind
    {
        DelayOn,
        DelayOff,
        OnePulse,
        Measure,
        Time,
        Date
    }

    [Serializable]
    public class Timer : Logic
    {
        public Timer() { }

        override public void SaveProperties(NameValueCollection coll)
        {
            base.SaveProperties(coll);
            var fp = CultureInfo.GetCultureInfo("en-US");
            coll["Kind"] = String.Format(fp, "Timer.{0}", Kind);
            coll["Seconds"] = String.Format(fp, "{0}", Seconds);
        }

        private bool _loaded;

        public override void LoadProperties(NameValueCollection coll)
        {
            // далее загрузка свойств для типа Timer
            var akind = (coll["Kind"] ?? "").Split(new[] { '.' });
            if (akind.Length != 2 || akind[0] != "Timer") return;
            TimerKind kind;
            if (!Enum.TryParse(akind[1], out kind)) return;
            Kind = kind;
            base.LoadProperties(coll);
            uint seconds;
            if (uint.TryParse(coll["Seconds"] ?? "", out seconds))
                Seconds = seconds;
            _loaded = true;
        }    

        private DateTime _timerDealyOn = DateTime.MinValue;

        private DateTime _startTime = DateTime.MinValue;

        private bool _timerRun;

        private bool _timerStart;

        public uint Seconds { private get; set; }

        override public void Calculate()
        {
            if (!_loaded) return;
            for (uint i = 1; i <= InpCount; i++)
                if (Inputs[i].Value == null) Inputs[i].Value = false;
            bool input;
            DateTime atime;
            switch (_akind)
            {
                case TimerKind.DelayOn:
                    input = (bool)(Inputs[1].Value ?? false) ^ Inputs[1].Invert;
                    if (!input)
                        _timerStart = false;
                    if (_timerRun && !input)
                    {
                        _timerRun = false;
                        _timerDealyOn = DateTime.MinValue;
                        Outputs[1].Value = false ^ Outputs[1].Invert;
                    }
                    else if (!_timerRun)
                    {
                        if (input && !_timerStart)
                        {
                            _timerDealyOn = DateTime.Now + new TimeSpan(0, 0, 0, 0, (int)Seconds * 1000);
                            _timerRun = true;
                            _timerStart = true;
                        }
                        else
                            Outputs[1].Value = input ^ Outputs[1].Invert;
                    }
                    else if (_timerRun && input)
                    {
                        if (_timerDealyOn < DateTime.Now)
                        {
                            _timerRun = false;
                            _timerDealyOn = DateTime.MinValue;
                            Outputs[1].Value = true ^ Outputs[1].Invert;
                        }
                    }
                    break;
                case TimerKind.DelayOff:
                    input = !((bool)(Inputs[1].Value ?? false) ^ Inputs[1].Invert);
                    if (!input)
                        _timerStart = false;
                    if (_timerRun && !input)
                    {
                        _timerRun = false;
                        _timerDealyOn = DateTime.MinValue;
                        Outputs[1].Value = false ^ !Outputs[1].Invert;
                    }
                    else if (!_timerRun)
                    {
                        if (input && !_timerStart)
                        {
                            _timerDealyOn = DateTime.Now + new TimeSpan(0, 0, 0, 0, (int)Seconds * 1000);
                            _timerRun = true;
                            _timerStart = true;
                        }
                        else
                            Outputs[1].Value = input ^ !Outputs[1].Invert;
                    }
                    else if (_timerRun && input)
                    {
                        if (_timerDealyOn < DateTime.Now)
                        {
                            _timerRun = false;
                            _timerDealyOn = DateTime.MinValue;
                            Outputs[1].Value = true ^ !Outputs[1].Invert;
                        }
                    }
                    break;
                case TimerKind.OnePulse:
                    input = ((bool)(Inputs[1].Value ?? false) ^ Inputs[1].Invert);
                    if (!input)
                        _timerStart = false;
                    if (_timerRun && !input && _timerDone)
                    {
                        // зона при досрочном снятии входного сигнала до истечения времени таймера
                        _timerRun = false;
                        _timerDealyOn = DateTime.MinValue;
                        Outputs[1].Value = Outputs[1].Invert;
                    }
                    else 
                    if (!_timerRun)
                    {
                       // зона ожидания перед началом счёта
                        if (input && !_timerStart)
                        {
                            // зона в момент начала счёта, выполняется один раз
                            _timerDealyOn = DateTime.Now + new TimeSpan(0, 0, 0, 0, (int)Seconds * 1000);
                            _timerRun = true;
                            _timerStart = true;
                            _timerDone = false;
                        }
                        else
                            Outputs[1].Value = Outputs[1].Invert;
                    }
                    else if (_timerRun && (input || !_timerDone))
                    {
                        // зона ожидания при счёте времени таймера
                        Outputs[1].Value = !Outputs[1].Invert;
                        if (_timerDealyOn < DateTime.Now)
                        {
                            // зона в момент окончания счёта таймера, выполняется один раз
                            _timerDone = true;
                            _timerRun = false;
                            _timerDealyOn = DateTime.MinValue;
                            Outputs[1].Value = Outputs[1].Invert;
                        }
                    }
                    break;
                case TimerKind.Measure:
                    input = ((bool)(Inputs[1].Value ?? false) ^ Inputs[1].Invert);
                    if (!input)
                    {
                        if (_timerStart)
                        {
                            Outputs[1].Value = 0;
                            Outputs[2].Value = 0;
                            Outputs[3].Value = 0;
                            Outputs[4].Value = 0;
                            _timerStart = false;
                        }
                    }
                    TimeSpan time;
                    if (_timerRun && !input)
                    {
                        // зона при досрочном снятии входного сигнала до истечения времени таймера
                        _timerRun = false;
                        time = DateTime.Now - _startTime;
                        Outputs[1].Value = time.Milliseconds;
                        Outputs[2].Value = time.Seconds;
                        Outputs[3].Value = time.Minutes;
                        Outputs[4].Value = time.Hours;
                    }
                    else 
                    if (!_timerRun)
                    {
                       // зона ожидания перед началом счёта
                        if (input && !_timerStart)
                        {
                            // зона в момент начала счёта, выполняется один раз
                            _startTime = DateTime.Now;
                            _timerRun = true;
                            _timerStart = true;
                        }
                    }
                    else if (_timerRun && input)
                    {
                        // зона ожидания при счёте времени таймера
                        time = DateTime.Now - _startTime;
                        Outputs[1].Value = time.Milliseconds;
                        Outputs[2].Value = time.Seconds;
                        Outputs[3].Value = time.Minutes;
                        Outputs[4].Value = time.Hours;
                    }
                    break;
                case TimerKind.Time:
                        atime = DateTime.Now;
                        Outputs[1].Value = atime.Second;
                        Outputs[2].Value = atime.Minute;
                        Outputs[3].Value = atime.Hour;
                    break;
                case TimerKind.Date:
                        atime = DateTime.Now;
                        Outputs[1].Value = atime.Day;
                        Outputs[2].Value = atime.Month;
                        Outputs[3].Value = atime.Year;
                    break;
            }
            if (Outputs[1].Link != null)
                Outputs[1].Link.TransferLink(Outputs[1].Value);
            if (_akind != TimerKind.Measure && _akind != TimerKind.Time && _akind != TimerKind.Date) return;
            if (Outputs[2].Link != null)
                Outputs[2].Link.TransferLink(Outputs[2].Value);
            if (Outputs[3].Link != null)
                Outputs[3].Link.TransferLink(Outputs[3].Value);
            if (_akind == TimerKind.Time || _akind == TimerKind.Date) return;
            if (Outputs[4].Link != null)
                Outputs[4].Link.TransferLink(Outputs[4].Value);
        }

        private TimerKind _akind;
        private bool _timerDone;

        private TimerKind Kind
        {
            get { return _akind; }
            set
            {
                _akind = value;
                switch (_akind)
                {
                    case TimerKind.DelayOn:
                    case TimerKind.DelayOff:
                    case TimerKind.OnePulse:
                        InpCount = 1;
                        OutCount = 1;
                        Inputs[1].Value = false;
                        Outputs[1].Value = false;
                        CalcDrawingSize();
                        break;
                    case TimerKind.Measure:
                        InpCount = 1;
                        OutCount = 4;
                        Inputs[1].Value = false;
                        Inputs[1].Name = "START";
                        Outputs[1].Value = 0;
                        Outputs[2].Value = 0;
                        Outputs[3].Value = 0;
                        Outputs[4].Value = 0;
                        Outputs[1].Name = "MS";
                        Outputs[2].Name = "SE";
                        Outputs[3].Name = "MI";
                        Outputs[4].Name = "HR";
                        CalcDrawingSize();
                        break;
                    case TimerKind.Time:
                        InpCount = 0;
                        OutCount = 3;
                        Outputs[1].Value = 0;
                        Outputs[2].Value = 0;
                        Outputs[3].Value = 0;
                        Outputs[1].Name = "SEC";
                        Outputs[2].Name = "MIN";
                        Outputs[3].Name = "HOUR";
                        CalcDrawingSize();
                        break;
                    case TimerKind.Date:
                        InpCount = 0;
                        OutCount = 3;
                        Outputs[1].Value = 0;
                        Outputs[2].Value = 0;
                        Outputs[3].Value = 0;
                        Outputs[1].Name = "DAY";
                        Outputs[2].Name = "MON";
                        Outputs[3].Name = "YEAR";
                        CalcDrawingSize();
                        break;
                }
            }
        }

        // реализация интерфейса ICloneable
        override public object Clone()
        {
            var plot = new Timer(Plots, _akind) {Seconds = Seconds};
            return plot;
        }

        public Timer(PlotsOwner owner, TimerKind kind)
            : this(owner)
        {
            Kind = kind;
        }

        public Timer(PlotsOwner owner)
            : base(owner)
        {
            CalcDrawingSize();
        }

        override protected string FuncName()
        {
            var funcName = "(undef)";
            switch (_akind)
            {
                case TimerKind.DelayOn:
                    funcName = "DLY On";
                    break;
                case TimerKind.DelayOff:
                    funcName = "DLY Off";
                    break;
                case TimerKind.OnePulse:
                    funcName = "PULSE";
                    break;
                case TimerKind.Measure:
                    funcName = "MEASU";
                    break;
                case TimerKind.Time:
                    funcName = "TIME";
                    break;
                case TimerKind.Date:
                    funcName = "DATE";
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
                    if (_akind == TimerKind.Measure || _akind == TimerKind.Time || _akind == TimerKind.Date) break;
                    popup.Items.Add(new ToolStripSeparator());
                    var item = new ToolStripMenuItem("Увеличить время (+1 s)");
                    item.Click += (sender, args) =>
                        {
                            Seconds++;
                            Refresh(true);
                        };
                    popup.Items.Add(item);
                    item = new ToolStripMenuItem("Уменьшить время (-1 s)");
                    item.Click += (sender, args) =>
                        {
                            if (Seconds > 0) Seconds--;
                            Refresh(true);
                        };
                    popup.Items.Add(item);
                    break;
            }
        }

        public override void DrawAt(Graphics g)
        {
            base.DrawAt(g);
            var rect = Bounds;
            if (rect.IsEmpty) return;
            if (_akind == TimerKind.Measure || _akind == TimerKind.Time || _akind == TimerKind.Date) return;
            var timerect = new RectangleF(rect.Location, new SizeF(rect.Width, Height));
            timerect.Offset(0, Height/2);
            var fp = CultureInfo.GetCultureInfo("en-US");
            var value = Plots.Emulation
                            ? String.Format("{0} с", _timerRun
                                                         ? Math.Round(
                                                             (_timerDealyOn - DateTime.Now).TotalMilliseconds/1000, 1)
                                                               .ToString("0.0", fp)
                                                         : Seconds.ToString("0.0", fp))
                            : String.Format("{0} с", Seconds.ToString("0", fp)); 
            using (var font = new Font("Arial Narrow", 8f))
            {
                g.DrawString(value, font, SystemBrushes.WindowText, timerect,
                             new StringFormat
                                 {
                                     Alignment = StringAlignment.Center,
                                     LineAlignment = StringAlignment.Center
                                 });
            }
        }

        override public uint InpCount // количество входов
        {
            get { return _akind == TimerKind.Time || _akind == TimerKind.Date ? 0 : base.InpCount; }
            protected set { base.InpCount = value; }
        }
   
    }
}
