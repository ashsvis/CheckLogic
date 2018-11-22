using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CheckLogic
{
    public enum MathKind
    {
// ReSharper disable InconsistentNaming
        LMT,
        ABS,
        NEG,
        ADD,
        SUB,
        MUL,
        DIV,
        MOD,
        EXP,
        LN,
        LOG,
        POW,
        SQRT,
        ROND,
        TRNC,
        MIN,
        MAX,
        AVG,
        RLAVG
        // ReSharper restore InconsistentNaming
    }

    [Serializable]
    public class MathCalc : Logic
    {
        public MathCalc() { }

        override public void SaveProperties(NameValueCollection coll)
        {
            base.SaveProperties(coll);
            var fp = CultureInfo.GetCultureInfo("en-US");
            coll["Kind"] = String.Format(fp, "MathCalc.{0}", Kind);
        }

        private bool _loaded;

        public override void LoadProperties(NameValueCollection coll)
        {
            // далее загрузка свойств для типа Comparator
            var akind = (coll["Kind"] ?? "").Split(new[] { '.' });
            if (akind.Length != 2 || akind[0] != "MathCalc") return;
            MathKind kind;
            if (!Enum.TryParse(akind[1], out kind)) return;
            Kind = kind;
            base.LoadProperties(coll);
            _loaded = true;
        }

        private readonly List<Tuple<DateTime, float>> _floats = new List<Tuple<DateTime, float>>(); 

        public override void Calculate()
        {
            if (!_loaded || Outputs.Length != 1) return;
            var value = (float)(Inputs[1].Value ?? 0f);
            var dtm = DateTime.Now;
            var tick = false;
            if (_second != dtm.Second)
            {
                _second = dtm.Second;
                tick = true;
            }
            float secondvalue;
            switch (_akind)
            {
                case MathKind.LMT:
                    var minval = (float) (Inputs[2].Value ?? 0f);
                    var maxval = (float) (Inputs[3].Value ?? 0f);
                    if (value < minval)
                        Outputs[1].Value = minval;
                    else if (value > maxval)
                        Outputs[1].Value = maxval;
                    else
                        Outputs[1].Value = value;
                    break;
                case MathKind.ABS:
                    Outputs[1].Value = Math.Abs(value);
                    break;
                case MathKind.NEG:
                    Outputs[1].Value = -1f * value;
                    break;
                case MathKind.EXP:
                    Outputs[1].Value = (float)Math.Exp(value);
                    break;
                case MathKind.LN:
                    Outputs[1].Value = (float)Math.Log(value);
                    break;
                case MathKind.LOG:
                    Outputs[1].Value = (float)Math.Log10(value);
                    break;
                case MathKind.SQRT:
                    Outputs[1].Value = (float)Math.Sqrt(value);
                    break;
                case MathKind.ROND:
                    Outputs[1].Value = (float)Math.Round(value);
                    break;
                case MathKind.TRNC:
                    Outputs[1].Value = (float)Math.Truncate(value);
                    break;
                case MathKind.RLAVG:
                    if (tick)
                    {
                        _floats.RemoveAll(item => item.Item1 < DateTime.Now - new TimeSpan(0, 0, 0, 10)); // удаляем старее 10-х секунд
                        if (float.IsNaN(value) || float.IsInfinity(value) ||
                            float.IsNegativeInfinity(value) || float.IsPositiveInfinity(value)) break;
                        _floats.Add(new Tuple<DateTime, float>(DateTime.Now, value));
                    }
                    Outputs[1].Value = _floats.Average(item => item.Item2);
                    break;
                case MathKind.ADD:
                    secondvalue = (float)(Inputs[2].Value ?? 0f);
                    Outputs[1].Value = value + secondvalue;
                    break;
                case MathKind.SUB:
                    secondvalue = (float)(Inputs[2].Value ?? 0f);
                    Outputs[1].Value = value - secondvalue;
                    break;
                case MathKind.MUL:
                    secondvalue = (float)(Inputs[2].Value ?? 0f);
                    Outputs[1].Value = value * secondvalue;
                    break;
                case MathKind.MIN:
                    secondvalue = (float)(Inputs[2].Value ?? 0f);
                    Outputs[1].Value = Math.Min(value, secondvalue);
                    break;
                case MathKind.MAX:
                    secondvalue = (float)(Inputs[2].Value ?? 0f);
                    Outputs[1].Value = Math.Max(value, secondvalue);
                    break;
                case MathKind.DIV:
                    secondvalue = (float)(Inputs[2].Value ?? 0f);
                    Outputs[1].Value = Math.Abs(secondvalue) < float.Epsilon
                                           ? float.PositiveInfinity
                                           : value/secondvalue;
                    break;
                case MathKind.MOD:
                    secondvalue = (float)(Inputs[2].Value ?? 0f);
                    var val = Math.Abs(value);
                    var div = Math.Abs(secondvalue);
                    Outputs[1].Value = Math.Sign(secondvalue) * (val - div * Math.Truncate(val / div));
                    break;
                case MathKind.POW:
                    secondvalue = (float)(Inputs[2].Value ?? 0f);
                    Outputs[1].Value = (float)Math.Pow(value, secondvalue);
                    break;
                case MathKind.AVG:
                    secondvalue = (float)(Inputs[2].Value ?? 0f);
                    Outputs[1].Value = (value + secondvalue) / 2f;
                    break;
            }
            if (Outputs[1].Link != null)
                Outputs[1].Link.TransferLink(Outputs[1].Value);
        }

        private MathKind _akind;
        private int _second;

        private MathKind Kind
        {
            get { return _akind; }
            set
            {
                _akind = value;
                switch (_akind)
                {
                    case MathKind.LMT:
                        InpCount = 3;
                        OutCount = 1;
                        Inputs[2].Name = "Min";
                        Inputs[3].Name = "Max";
                        break;
                    case MathKind.ABS:
                    case MathKind.NEG:
                    case MathKind.EXP:
                    case MathKind.LN:
                    case MathKind.LOG:
                    case MathKind.SQRT:
                    case MathKind.ROND:
                    case MathKind.TRNC:
                    case MathKind.RLAVG:
                        InpCount = 1;
                        OutCount = 1;
                        break;
                    case MathKind.ADD:
                    case MathKind.SUB:
                    case MathKind.MUL:
                    case MathKind.MIN:
                    case MathKind.MAX:
                    case MathKind.AVG:
                        InpCount = 2;
                        OutCount = 1;
                        break;
                    case MathKind.DIV:
                    case MathKind.MOD:
                        InpCount = 2;
                        OutCount = 1;
                        Inputs[1].Name = "N";
                        Inputs[2].Name = "D";
                        break;
                    case MathKind.POW:
                        InpCount = 2;
                        OutCount = 1;
                        Inputs[1].Name = "X";
                        Inputs[2].Name = "Y";
                        break;
                }
                CalcDrawingSize();
                for (uint i = 1; i <= InpCount; i++)
                    Inputs[i].Value = 0f;
                Outputs[1].Value = 0f;
            }
        }

        // реализация интерфейса ICloneable
        override public object Clone()
        {
            var plot = new MathCalc(Plots, _akind);
            return plot;
        }

        public MathCalc(PlotsOwner owner, MathKind kind)
            : this(owner)
        {
            Kind = kind;
        }

        public MathCalc(PlotsOwner owner)
            : base(owner)
        {
            CalcDrawingSize();
        }

        override protected string FuncName()
        {
            return _akind.ToString();
        }

        override protected bool InvertEnabled(PlotHits hits, uint inOut)
        {
            return false;
        }
    }
}
