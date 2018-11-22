using System;
using System.Collections.Specialized;
using System.Globalization;

namespace CheckLogic
{
    public enum GeneratorKind
    {
        DigitalMeandre,
        AnalogMeandre,
        Growing,
        Waning
    }

    [Serializable]
    public class Generator : Logic
    {
        public Generator()
        {
            _oldtime = DateTime.Now;
        }

        override public void SaveProperties(NameValueCollection coll)
        {
            base.SaveProperties(coll);
            var fp = CultureInfo.GetCultureInfo("en-US");
            coll["CycleTime"] = String.Format(fp, "{0}", CycleTime);
            coll["CycleDuty"] = String.Format(fp, "{0}", CycleDuty < float.Epsilon ? CycleDuty : 0f);
            coll["Kind"] = String.Format(fp, "Generator.{0}", Kind);
            coll["OnValue"] = String.Format(fp, "{0}", OnValue);
            coll["OffValue"] = String.Format(fp, "{0}", OffValue);
        }

        private float CycleDuty
        {
            get { return _cycleDuty; }
            set { _cycleDuty = value; }
        }

        private float CycleTime
        {
            get { return _cycleTime; }
            set { _cycleTime = value; }
        }

        private float OnValue
        {
            get { return _onValue; }
            set { _onValue = value; }
        }

        private float OffValue
        {
            get { return _offValue; }
            set { _offValue = value; }
        }

        private bool _loaded;

        public override void LoadProperties(NameValueCollection coll)
        {
            // далее загрузка свойств для типа Timer
            var akind = (coll["Kind"] ?? "").Split(new[] { '.' });
            if (akind.Length != 2 || akind[0] != "Generator") return;
            GeneratorKind kind;
            if (!Enum.TryParse(akind[1], out kind)) return;
            Kind = kind;
            var fp = CultureInfo.GetCultureInfo("en-US");
            base.LoadProperties(coll);
            float value;
            if (float.TryParse(coll["CycleTime"] ?? "", NumberStyles.Float, fp, out value))
                CycleTime = value;
            if (float.TryParse(coll["CycleDuty"] ?? "", NumberStyles.Float, fp, out value))
                CycleDuty = value;
            if (float.TryParse(coll["OnValue"] ?? "", NumberStyles.Float, fp, out value))
                OnValue = value;
            if (float.TryParse(coll["OffValue"] ?? "", NumberStyles.Float, fp, out value))
                OffValue = value;
            _loaded = true;
        }

        private bool _state;

        private float _value;

        private DateTime _oldtime;

        public override void Calculate()
        {
            if (!_loaded || Outputs.Length != 1) return;
            var onetime = (DateTime.Now - _oldtime).Milliseconds;
            _oldtime = DateTime.Now;
            for (uint i = 1; i <= InpCount; i++)
                if (Inputs[i].Value == null) Inputs[i].Value = false;
            var input = (bool)(Inputs[1].Value ?? false) ^ Inputs[1].Invert;
            var cycle = (float)(Inputs[2].Value ?? _cycleTime) * 1000f;
            var duty = (float)(Inputs[3].Value ?? _cycleDuty);
            var koeff = onetime / cycle;
            if (!_state && _timeState < DateTime.Now)
            {
                _state = true;
                var time = Convert.ToInt32(cycle * duty);
                _timeState = DateTime.Now + new TimeSpan(0, 0, 0, 0, time);
                if (_akind == GeneratorKind.Growing)
                    _value = (float)(Inputs[5].Value ?? _offValue); // min
                if (_akind == GeneratorKind.Waning)
                    _value = (float)(Inputs[4].Value ?? _onValue); // max
            }
            float max;
            float min;
            float part;
            switch (_akind)
            {
                case GeneratorKind.DigitalMeandre:
                    Outputs[1].Value = (input && _state) ^ Outputs[1].Invert;
                    break;
                case GeneratorKind.AnalogMeandre:
                    max = (float)(Inputs[4].Value ?? _onValue);
                    min = (float)(Inputs[5].Value ?? _offValue);
                    Outputs[1].Value = input ? (_state ? max : min) : 0f;
                    break;
                case GeneratorKind.Growing:
                    max = (float)(Inputs[4].Value ?? _onValue);
                    min = (float)(Inputs[5].Value ?? _offValue);
                    part = Math.Abs(max - min) * koeff;
                    Outputs[1].Value = input ? (_value) : 0f;
                    _value += part;
                    break;
                case GeneratorKind.Waning:
                    max = (float)(Inputs[4].Value ?? _onValue);
                    min = (float)(Inputs[5].Value ?? _offValue);
                    part = Math.Abs(max - min) * koeff;
                    Outputs[1].Value = input ? (_value) : 0f;
                    _value -= part;
                    break;
            }
            if (_state && _timeState < DateTime.Now)
            {
                _state = false;
                var time = Convert.ToInt32(cycle - cycle * duty);
                _timeState = DateTime.Now + new TimeSpan(0, 0, 0, 0, time);
            }
            if (Outputs[1].Link != null)
                Outputs[1].Link.TransferLink(Outputs[1].Value);
        }

        private GeneratorKind _akind;
        private DateTime _timeState;
        private float _onValue = 100f;
        private float _offValue;
        private float _cycleTime = 1.0f;
        private float _cycleDuty = 0.5f;

        private GeneratorKind Kind
        {
            get { return _akind; }
            set
            {
                _akind = value;
                switch (_akind)
                {
                    case GeneratorKind.DigitalMeandre:
                        InpCount = 3;
                        OutCount = 1;
                        CalcDrawingSize();
                        Inputs[1].Value = false;
                        Inputs[1].Name = "RUN";
                        Inputs[2].Value = _cycleTime;
                        Inputs[2].Name = "CYCLE";
                        Inputs[3].Value = _cycleDuty;
                        Inputs[3].Name = "DUTY";

                        Outputs[1].Value = false;
                        break;
                    case GeneratorKind.AnalogMeandre:
                    case GeneratorKind.Growing:
                    case GeneratorKind.Waning:
                        InpCount = 5;
                        OutCount = 1;
                        CalcDrawingSize();
                        Inputs[1].Value = false;
                        Inputs[1].Name = "RUN";
                        Inputs[2].Value = _cycleTime;
                        Inputs[2].Name = "CYCLE";
                        Inputs[3].Value = _cycleDuty;
                        Inputs[3].Name = "DUTY";
                        Inputs[3].Invisible = _akind == GeneratorKind.Growing || _akind == GeneratorKind.Waning;
                        Inputs[4].Value = _onValue;
                        Inputs[4].Name = "MAX";
                        Inputs[5].Value = _offValue;
                        Inputs[5].Name = "MIN";
                        Outputs[1].Value = 0f;
                        break;
                }
            }
        }

        // реализация интерфейса ICloneable
        public override object Clone()
        {
            var plot = new Generator(Plots, _akind)
                {
                    CycleTime = CycleTime,
                    CycleDuty = CycleDuty,
                    OnValue = OnValue,
                    OffValue = OffValue
                };
            return plot;
        }

        public Generator(PlotsOwner owner, GeneratorKind kind)
            : this(owner)
        {
            Kind = kind;
        }

        public Generator(PlotsOwner owner)
            : base(owner)
        {
            CalcDrawingSize();
        }

        override protected string FuncName()
        {
            var funcName = "(undef)";
            switch (_akind)
            {
                case GeneratorKind.DigitalMeandre:
                    funcName = "DMEA";
                    break;
                case GeneratorKind.AnalogMeandre:
                    funcName = "AMEA";
                    break;
                case GeneratorKind.Growing:
                    funcName = "GROW";
                    break;
                case GeneratorKind.Waning:
                    funcName = "WAN";
                    break;
            }
            return funcName;
        }

        override protected bool InvertEnabled(PlotHits hits, uint inOut)
        {
            return hits == PlotHits.InputLink || _akind ==  GeneratorKind.DigitalMeandre;
        }

        override protected bool HitEnabled(PlotHits hits, uint inOut = 0)
        {
            return hits != PlotHits.InputLink || (_akind != GeneratorKind.Growing && _akind != GeneratorKind.Waning) || inOut != 3;
        }
    }
}
