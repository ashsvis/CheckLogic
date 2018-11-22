using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CheckLogic
{
    public enum SelectKind
    {
        // ReSharper disable InconsistentNaming
        Digital,
        Analog
        // ReSharper restore InconsistentNaming
    }

    [Serializable]
    public class Selector : Logic
    {
        public Selector() { }

        override public void SaveProperties(NameValueCollection coll)
        {
            base.SaveProperties(coll);
            var fp = CultureInfo.GetCultureInfo("en-US");
            coll["Kind"] = String.Format(fp, "Selector.{0}", Kind);
        }

        private bool _loaded;

        public override void LoadProperties(NameValueCollection coll)
        {
            // далее загрузка свойств для типа Comparator
            var akind = (coll["Kind"] ?? "").Split(new[] { '.' });
            if (akind.Length != 2 || akind[0] != "Selector") return;
            SelectKind kind;
            if (!Enum.TryParse(akind[1], out kind)) return;
            Kind = kind;
            base.LoadProperties(coll);
            _loaded = true;
        }

        public override void Calculate()
        {
            if (!_loaded || Outputs.Length != 1) return;
            var value = (bool)(Inputs[1].Value ?? false) ^ Inputs[1].Invert;
            switch (_akind)
            {
                case SelectKind.Digital:
                    Outputs[1].Value = (value 
                        ? (bool)(Inputs[3].Value ?? false) ^ Inputs[3].Invert 
                        : (bool)(Inputs[2].Value ?? false) ^ Inputs[2].Invert) ^ Outputs[1].Invert;
                    break;
                case SelectKind.Analog:
                    Outputs[1].Value = value ? (float)(Inputs[3].Value ?? 0f) : (float)(Inputs[2].Value ?? 0f);
                    break;
            }
            if (Outputs[1].Link != null)
                Outputs[1].Link.TransferLink(Outputs[1].Value);
        }

        private SelectKind _akind;

        private SelectKind Kind
        {
            get { return _akind; }
            set
            {
                _akind = value;
                InpCount = 3;
                OutCount = 1;
                CalcDrawingSize();
                Inputs[1].Value = false;
                //Inputs[2].Name = GetOffValue();
                //Inputs[3].Name = GetOnValue();
                switch (_akind)
                {
                    case SelectKind.Digital:
                        for (uint i = 2; i <= InpCount; i++)
                            Inputs[i].Value = false;
                        Outputs[1].Value = false;
                        break;
                    case SelectKind.Analog:
                        for (uint i = 2; i <= InpCount; i++)
                            Inputs[i].Value = 0f;
                        Outputs[1].Value = 0f;
                        break;
                }
            }
        }

        // реализация интерфейса ICloneable
        override public object Clone()
        {
            var plot = new Selector(Plots, _akind);
            return plot;
        }

        public Selector(PlotsOwner owner, SelectKind kind)
            : this(owner)
        {
            Kind = kind;
        }

        public Selector(PlotsOwner owner)
            : base(owner)
        {
            CalcDrawingSize();
        }

        override protected string FuncName()
        {
            var funcName = "(undef)";
            switch (_akind)
            {
                case SelectKind.Digital:
                    funcName = "SEL";
                    break;
                case SelectKind.Analog:
                    funcName = "SEL";
                    break;
            }
            return funcName;
        }

        override protected bool InvertEnabled(PlotHits hits, uint inOut)
        {
            return _akind == SelectKind.Digital || hits == PlotHits.InputLink && inOut == 1;
        }
    }
}
