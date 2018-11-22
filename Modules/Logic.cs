using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CheckLogic
{
    public enum LogicKind
    {
        Not,
        Or2,
        Or3,
        Or4,
        Or5,
        Or6,
        Or7,
        Or8,
        And2,
        And3,
        And4,
        And5,
        And6,
        And7,
        And8,
        Xor,
        RsTrigger,
        SrTrigger,
        FrontEdge
    }

    public enum EdgeKind
    {
        None,
        Left
    }

    [Serializable]
    public class Logic : Plot
    {
        public Logic() { }

        override public void SaveProperties(NameValueCollection coll)
        {
            base.SaveProperties(coll);
            var fp = CultureInfo.GetCultureInfo("en-US");
            coll["Kind"] = String.Format(fp, "Logic.{0}", Kind);
            var vals = new List<string>();
            for (uint i = 1; i <= Inputs.Length; i++)
                vals.Add(Inputs[i] == null ? "" : String.Format(fp, "{0}", Inputs[i].Value));
            coll["Inputs"] = String.Join(",", vals);
            vals.Clear();
            for (uint i = 1; i <= Outputs.Length; i++)
                vals.Add(Outputs[i] == null ? "" : String.Format(fp, "{0}", Outputs[i].Value));
            coll["Outputs"] = String.Join(",", vals);
            vals.Clear();
            for (uint i = 1; i <= Inputs.Length; i++)
                vals.Add(Inputs[i].Invert ? "1" : "0");
            coll["InputsInvert"] = String.Join(",", vals);
            vals.Clear();
            for (uint i = 1; i <= Outputs.Length; i++)
                vals.Add(Outputs[i].Invert ? "1" : "0");
            coll["OutputsInvert"] = String.Join(",", vals);
            for (uint i = 1; i <= OutCount; i++)
            {
                if (Outputs[i].Link == null) continue;
                var targets = new List<string>();
                var startpoints = new List<string>();
                var endpoints = new List<string>();
                for (var j = 0; j < Outputs[i].Link.Targets.Count(); j++)
                {
                    var targetInfo = Outputs[i].Link.Targets[j];
                    var target = String.Format(fp, "{0}.{1}.{2}",
                                               (PageNum != targetInfo.Module.PageNum) 
                                               ? targetInfo.Module.PageNum.ToString("0") : "*",
                                               targetInfo.Module.OrderNum,
                                               targetInfo.Pin);
                    targets.Add(target);
                    var startpoint = String.Format(fp, "{0}:{1}", targetInfo.FirstPoint.X, targetInfo.FirstPoint.Y);
                    startpoints.Add(startpoint);
                    var endpoint = String.Format(fp, "{0}:{1}", targetInfo.LastPoint.X, targetInfo.LastPoint.Y);
                    endpoints.Add(endpoint);
                }
                coll["OutputLink" + i] = String.Join(",", targets);
                coll["FirstPoints" + i] = String.Join(",", startpoints);
                coll["LastPoints" + i] = String.Join(",", endpoints);
            }
        }

        private bool _loaded;

        public override void LoadProperties(NameValueCollection coll)
        {
            base.LoadProperties(coll);
            // загрузка связей
            OutputLinks.Clear();
            for (uint i = 1; i <= OutCount; i++)
            {
                var link = coll["OutputLink" + i];
                if (link == null) break;
                OutputLinks.Add(new LinkInfo
                                {
                                    OutputLinks = link,
                                    FirstPoints = coll["FirstPoints" + i],
                                    LastPoints = coll["LastPoints" + i]
                                }
                );
            }
            var aInpInvert = (coll["InputsInvert"] ?? "").Split(new[] { ',' });
            for (uint i = 0; i < aInpInvert.Length; i++)
                Inputs[i + 1].Invert = aInpInvert[i] == "1";
            var aOutInvert = (coll["OutputsInvert"] ?? "").Split(new[] { ',' });
            for (uint i = 0; i < aOutInvert.Length; i++)
                Outputs[i + 1].Invert = aOutInvert[i] == "1";
            var aInputs = (coll["Inputs"] ?? "").Split(new[] { ',' });
            var fp = CultureInfo.GetCultureInfo("en-US");
            for (uint i = 0; i < aInputs.Length; i++)
                if (i < Inputs.Length)
                {
                    bool b;
                    if (bool.TryParse(aInputs[i], out b))
                        Inputs[i + 1].Value = b;
                    float value;
                    if (float.TryParse(aInputs[i], NumberStyles.Float, fp, out value))
                        Inputs[i + 1].Value = value;
                }
            // далее загрузка свойств для типа Logic
            var akind = (coll["Kind"] ?? "").Split(new[] { '.' });
            if (akind.Length != 2 || akind[0] != "Logic") return;
            LogicKind kind;
            if (!Enum.TryParse(akind[1], out kind)) return;
            Kind = kind;
            _loaded = true;
        }

        private void ReorderPlots()
        {
            if (Plots == null) return;
            var n = 1;
            foreach (var plot in Plots)
                plot.OrderNum = (uint)n++;
        }

        public override void ShowPopupAt(Control parent, PointF point)
        {
            PopupMenu.Items.Clear();
            var info = CheckMouseHitAt(point);
            if (info.Hits == PlotHits.None) return;
            ToolStripMenuItem item;
            switch (info.Hits)
            {
                case PlotHits.OrderNum:
                    if (Plots.Emulation) break;
                    item = new ToolStripMenuItem("Изменить номер...");
                    item.Click += (sender, args) =>
                        {
                            var frm = new InputValueForm {IntValue = Convert.ToInt32(OrderNum)};
                            if (frm.ShowDialog() != DialogResult.OK) return;
                            var n = frm.IntValue;
                            if (Plots == null || n <= 0 || n >= Plots.Count) return;
                            Plots.Remove(this);
                            Plots.Insert(n - 1, this);
                            ReorderPlots();
                            Refresh(true);
                        };
                    PopupMenu.Items.Add(item);
                    break;
                case PlotHits.Caption:
                    AddPopupItems(PopupMenu, info);
                    break;
                case PlotHits.InputLink:
                    if (Plots.Emulation) break;
                    if (InvertEnabled(info.Hits, info.InOut))
                    {
                        item =
                            new ToolStripMenuItem("Инвертировать вход " + info.InOut)
                                {
                                    Tag = new Tuple<Plot, uint>(this, info.InOut)
                                };
                        item.Click += (o, args) =>
                            {
                                var param = (Tuple<Plot, uint>) ((ToolStripMenuItem) o).Tag;
                                var plot = param.Item1;
                                if (plot == null) return;
                                var inpindex = param.Item2;
                                if (inpindex < 1 || inpindex > 32) return;
                                plot.Inputs[inpindex].Invert = !plot.Inputs[inpindex].Invert;
                                Refresh(true);
                            };
                        PopupMenu.Items.Add(item);
                    }
                    if (info.InOut >= 1 && info.InOut <= 32 && Inputs[info.InOut].Link != null)
                    {
                        item = new ToolStripMenuItem("Удалить связь по входу") {Tag = this};
                        item.Click += (o, args) =>
                            {
                                var plot = (Plot) ((ToolStripMenuItem) o).Tag;
                                plot.RemoveSourceLink(info.InOut);
                                Refresh(true);
                            };
                        PopupMenu.Items.Add(item);
                    }
                    var removedlogic = new[]
                        {
                            LogicKind.Or3,
                            LogicKind.Or4,
                            LogicKind.Or5,
                            LogicKind.Or6,
                            LogicKind.Or7,
                            LogicKind.Or8,
                            LogicKind.And3,
                            LogicKind.And4,
                            LogicKind.And5,
                            LogicKind.And6,
                            LogicKind.And7,
                            LogicKind.And8
                        };
                    if (removedlogic.Contains(_akind))
                    {
                        PopupMenu.Items.Add(new ToolStripSeparator());
                        item = new ToolStripMenuItem("Удалить вход " + info.InOut + " (с удалением связей)")
                            {
                                Tag = this
                            };
                        item.Click += (o, args) =>
                            {
                                var plot = (Plot) ((ToolStripMenuItem) o).Tag;
                                plot.RemoveSourceLink(info.InOut);
                                // переназначение связей при сдвижке входов
                                for (var input = info.InOut + 1; input <= InpCount; input++)
                                {
                                    plot.Inputs[input - 1] = plot.Inputs[input];
                                    var source = plot.Inputs[input - 1].Link;
                                    if (source == null) continue;
                                    if (source.Module.Outputs[source.Pin].Link == null) continue;
                                    var modepin = source.Module.Outputs[source.Pin].Link.Targets.Find(module =>
                                                                                                      module.Module ==
                                                                                                      this &&
                                                                                                      module.Pin ==
                                                                                                      input);
                                    if (modepin == null) continue;
                                    modepin.Pin = input - 1;
                                }
                                // изменение сигнатуры на меньшую                            
                                switch (Kind)
                                {
                                    case LogicKind.Or3:
                                        Kind = LogicKind.Or2;
                                        break;
                                    case LogicKind.Or4:
                                        Kind = LogicKind.Or3;
                                        break;
                                    case LogicKind.Or5:
                                        Kind = LogicKind.Or4;
                                        break;
                                    case LogicKind.Or6:
                                        Kind = LogicKind.Or5;
                                        break;
                                    case LogicKind.Or7:
                                        Kind = LogicKind.Or6;
                                        break;
                                    case LogicKind.Or8:
                                        Kind = LogicKind.Or7;
                                        break;
                                    case LogicKind.And3:
                                        Kind = LogicKind.And2;
                                        break;
                                    case LogicKind.And4:
                                        Kind = LogicKind.And3;
                                        break;
                                    case LogicKind.And5:
                                        Kind = LogicKind.And4;
                                        break;
                                    case LogicKind.And6:
                                        Kind = LogicKind.And5;
                                        break;
                                    case LogicKind.And7:
                                        Kind = LogicKind.And6;
                                        break;
                                    case LogicKind.And8:
                                        Kind = LogicKind.And7;
                                        break;
                                }

                                Refresh(true);
                            };
                        PopupMenu.Items.Add(item);
                    }
                    break;
                case PlotHits.OutputLink:
                    if (Plots.Emulation) break;
                    if (InvertEnabled(info.Hits, info.InOut))
                    {
                        item =
                            new ToolStripMenuItem("Инвертировать выход " + info.InOut)
                                {
                                    Tag = new Tuple<Plot, uint>(this, info.InOut)
                                };
                        item.Click += (o, args) =>
                            {
                                var param = (Tuple<Plot, uint>) ((ToolStripMenuItem) o).Tag;
                                var plot = param.Item1;
                                if (plot == null) return;
                                var outindex = param.Item2;
                                if (outindex < 1 || outindex > 32) return;
                                plot.Outputs[outindex].Invert = !plot.Outputs[outindex].Invert;
                                Refresh(true);
                            };
                        PopupMenu.Items.Add(item);
                    }
                    if (info.InOut >= 1 && info.InOut <= 32 && Outputs[info.InOut].Link != null)
                    {
                        item = new ToolStripMenuItem("Удалить все связи по выходу") {Tag = this};
                        item.Click += (o, args) =>
                            {
                                var plot = (Plot) ((ToolStripMenuItem) o).Tag;
                                plot.RemoveTargetLinkFor(plot);
                                Refresh(true);
                            };
                        PopupMenu.Items.Add(item);
                    }
                    break;
                case PlotHits.Body:
                    if (Plots.Emulation)
                    {
                        AddPopupItems(PopupMenu, info);
                        break;
                    }
                    item = new ToolStripMenuItem("Идентификатор...");
                    item.Click += (sender, args) =>
                        {
                            var frm = new InputValueForm {StrValue = Name};
                            if (frm.ShowDialog() != DialogResult.OK) return;
                            var text = frm.StrValue;
                            if (Plots != null && Plots.FirstOrDefault(module => module.Name == text) == null)
                            {
                                Name = text.Length != 0 ? text : null;
                                Refresh(true);
                            }
                            else if (Name != text)
                                MessageBox.Show(String.Format("Новое имя элемента \"{0}\" дублируется!", text),
                                                @"Идентификатор элемента", MessageBoxButtons.OK,
                                                MessageBoxIcon.Error);
                        };
                    PopupMenu.Items.Add(item);
                    var addedlogic = new[]
                        {
                            LogicKind.Or2,
                            LogicKind.Or3,
                            LogicKind.Or4,
                            LogicKind.Or5,
                            LogicKind.Or6,
                            LogicKind.Or7,
                            LogicKind.And2,
                            LogicKind.And3,
                            LogicKind.And4,
                            LogicKind.And5,
                            LogicKind.And6,
                            LogicKind.And7
                        };
                    if (addedlogic.Contains(_akind))
                    {
                        PopupMenu.Items.Add(new ToolStripSeparator());
                        item = new ToolStripMenuItem("Добавить вход");
                        item.Click += (sender, args) =>
                            {
                                switch (Kind)
                                {
                                    case LogicKind.Or2:
                                        Kind = LogicKind.Or3;
                                        break;
                                    case LogicKind.Or3:
                                        Kind = LogicKind.Or4;
                                        break;
                                    case LogicKind.Or4:
                                        Kind = LogicKind.Or5;
                                        break;
                                    case LogicKind.Or5:
                                        Kind = LogicKind.Or6;
                                        break;
                                    case LogicKind.Or6:
                                        Kind = LogicKind.Or7;
                                        break;
                                    case LogicKind.Or7:
                                        Kind = LogicKind.Or8;
                                        break;
                                    case LogicKind.And2:
                                        Kind = LogicKind.And3;
                                        break;
                                    case LogicKind.And3:
                                        Kind = LogicKind.And4;
                                        break;
                                    case LogicKind.And4:
                                        Kind = LogicKind.And5;
                                        break;
                                    case LogicKind.And5:
                                        Kind = LogicKind.And6;
                                        break;
                                    case LogicKind.And6:
                                        Kind = LogicKind.And7;
                                        break;
                                    case LogicKind.And7:
                                        Kind = LogicKind.And8;
                                        break;
                                }
                                Refresh(true);
                            };
                        PopupMenu.Items.Add(item);
                    }
                    PopupMenu.Items.Add(new ToolStripSeparator());
                    item = new ToolStripMenuItem("Удалить элемент");
                    item.Click += (sender, args) => Remove();
                    PopupMenu.Items.Add(item);
                    AddPopupItems(PopupMenu, info);
                    break;
            }
            var pt = Point.Ceiling(point);
            PopupMenu.Show(parent.PointToScreen(pt));
        }

        override public bool ClickAt(PointF point)
        {
            var info = CheckMouseHitAt(point);
            if (info.Hits == PlotHits.None) return false;
            var index = info.InOut;
            switch (info.Hits)
            {
                case PlotHits.InputLink:
                    if (Inputs[index].Link != null) break; // нельзя именять вручную связанный вход
                    if (Inputs[index].Value is bool)
                    {
                        Inputs[index].Value = !(bool) Inputs[index].Value;
                        Refresh();
                        return true;
                    }
                    if (Inputs[index].Value is float)
                    {
                        var frm = new InputValueForm {FloatValue = (float) Inputs[index].Value};
                        if (frm.ShowDialog() == DialogResult.OK)
                        {
                            Inputs[index].Value = frm.FloatValue;
                            Refresh();
                            return true;
                        }
                    }
                    break;
                case PlotHits.OutputLink:
                    MessageBox.Show(ModuleName());
                    break;
            }
            return false;
        }

        private bool _trigMemory;

        public override void Calculate()
        {
            if (!_loaded || Outputs.Length != 1) return;
            bool accum;
            bool setvalue;
            bool resvalue;
            switch (_akind)
            {
                case LogicKind.Not:
                    Outputs[1].Value = !(bool) (Inputs[1].Value ?? false);
                    break;
                case LogicKind.Or2:
                case LogicKind.Or3:
                case LogicKind.Or4:
                case LogicKind.Or5:
                case LogicKind.Or6:
                case LogicKind.Or7:
                case LogicKind.Or8:
                    accum = false;
                    for (uint i = 1; i <= InpCount; i++)
                    {
                        accum |= (bool) (Inputs[i].Value ?? false) ^ Inputs[i].Invert;
                    }
                    Outputs[1].Value = accum ^ Outputs[1].Invert;
                    break;
                case LogicKind.And2:
                case LogicKind.And3:
                case LogicKind.And4:
                case LogicKind.And5:
                case LogicKind.And6:
                case LogicKind.And7:
                case LogicKind.And8:
                    accum = true;
                    for (uint i = 1; i <= InpCount; i++)
                    {
                        accum &= (bool) (Inputs[i].Value ?? false) ^ Inputs[i].Invert;
                    }
                    Outputs[1].Value = accum ^ Outputs[1].Invert;
                    break;
                case LogicKind.Xor:
                    Outputs[1].Value = (bool) Inputs[1].Value ^ (bool) Inputs[2].Value;
                    break;
                case LogicKind.RsTrigger:
                    setvalue = (bool) (Inputs[1].Value ?? false) ^ Inputs[1].Invert;
                    if (setvalue) _trigMemory = true;
                    resvalue = (bool) (Inputs[2].Value ?? false) ^ Inputs[2].Invert;
                    if (resvalue) _trigMemory = false;
                    Outputs[1].Value = _trigMemory ^ Outputs[1].Invert;
                    break;
                case LogicKind.SrTrigger:
                    resvalue = (bool) (Inputs[2].Value ?? false) ^ Inputs[2].Invert;
                    if (resvalue) _trigMemory = false;
                    setvalue = (bool) (Inputs[1].Value ?? false) ^ Inputs[1].Invert;
                    if (setvalue) _trigMemory = true;
                    Outputs[1].Value = _trigMemory ^ Outputs[1].Invert;
                    break;
                case LogicKind.FrontEdge:
                    setvalue = (bool) (Inputs[1].Value ?? false) ^ Inputs[1].Invert;
                    Outputs[1].Value = (setvalue && !_trigMemory) ^ Outputs[1].Invert;
                    _trigMemory = setvalue;
                    break;
            }
            if (Outputs[1].Link != null)
                Outputs[1].Link.TransferLink(Outputs[1].Value);
        }

        private LogicKind _akind;

        private LogicKind Kind
        {
            get { return _akind; } 
            set
            {
                _akind = value;
                switch (_akind)
                {
                    case LogicKind.Not:
                    case LogicKind.FrontEdge:
                        InpCount = 1;
                        OutCount = 1;
                        CalcDrawingSize();
                        break;
                    case LogicKind.Or2:
                    case LogicKind.And2:
                    case LogicKind.Xor:
                        InpCount = 2;
                        OutCount = 1;
                        CalcDrawingSize();
                        break;
                    case LogicKind.Or3:
                    case LogicKind.And3:
                        InpCount = 3;
                        OutCount = 1;
                        CalcDrawingSize();
                        break;
                    case LogicKind.Or4:
                    case LogicKind.And4:
                        InpCount = 4;
                        OutCount = 1;
                        CalcDrawingSize();
                        break;
                    case LogicKind.Or5:
                    case LogicKind.And5:
                        InpCount = 5;
                        OutCount = 1;
                        CalcDrawingSize();
                        break;
                    case LogicKind.Or6:
                    case LogicKind.And6:
                        InpCount = 6;
                        OutCount = 1;
                        CalcDrawingSize();
                        break;
                    case LogicKind.Or7:
                    case LogicKind.And7:
                        InpCount = 7;
                        OutCount = 1;
                        CalcDrawingSize();
                        break;
                    case LogicKind.Or8:
                    case LogicKind.And8:
                        InpCount = 8;
                        OutCount = 1;
                        CalcDrawingSize();
                        break;
                    case LogicKind.RsTrigger:
                    case LogicKind.SrTrigger:
                        InpCount = 2;
                        OutCount = 1;
                        Inputs[1].Name = "S";
                        Inputs[2].Name = "R";
                        CalcDrawingSize();
                        break;
                }
                for (uint i = 1; i <= InpCount; i++)
                    if (Inputs[i].Value == null) Inputs[i].Value = false;
                for (uint i = 1; i <= OutCount; i++)
                    if (Outputs[i].Value == null) Outputs[i].Value = false;
            }
        }

        // реализация интерфейса ICloneable
        override public object Clone()
        {
            var plot = new Logic(Plots, _akind);
            var coll = new NameValueCollection();
            SaveProperties(coll);
            plot.LoadProperties(coll);
            return plot;
        }

        protected void CalcDrawingSize()
        {
            var count = Math.Max(InpCount, OutCount);
            var height = count < 2 ? BaseSize : BaseSize + (BaseSize / 2) * (count - 1);
            Size = new SizeF(BaseSize, height);
        }

        public Logic(PlotsOwner owner, LogicKind kind)
            : this(owner)
        {
            Kind = kind;
            if (kind == LogicKind.Not) 
                Outputs[1].Invert = true;
        }

        public Logic(PlotsOwner owner)
            : base(owner)
        {
            CalcDrawingSize();
        }

        override protected bool InvertEnabled(PlotHits hits, uint inOut)
        {
            // для элементов "NOT" и "XOR" инвертирование входов и выходов запрещено
            return (_akind != LogicKind.Not && _akind != LogicKind.Xor) ||
                (hits != PlotHits.InputLink && hits != PlotHits.OutputLink);
        }

        override protected string FuncName()
        {
            var funcName = "(undef)";
            switch (_akind)
            {
                case LogicKind.Not:
                case LogicKind.Or2:
                case LogicKind.Or3:
                case LogicKind.Or4:
                case LogicKind.Or5:
                case LogicKind.Or6:
                case LogicKind.Or7:
                case LogicKind.Or8:
                    funcName = "1";
                    break;
                case LogicKind.And2:
                case LogicKind.And3:
                case LogicKind.And4:
                case LogicKind.And5:
                case LogicKind.And6:
                case LogicKind.And7:
                case LogicKind.And8:
                    funcName = "&";
                    break;
                case LogicKind.Xor:
                    funcName = "=1";
                    break;
                case LogicKind.RsTrigger:
                    funcName = "RS";
                    break;
                case LogicKind.SrTrigger:
                    funcName = "SR";
                    break;
                case LogicKind.FrontEdge:
                    funcName = "FE";
                    break;
            }
            return funcName;
        }

        override protected void AddPopupItems(ContextMenuStrip popup, HitInfo hitinfo)
        {
            switch (hitinfo.Hits)
            {
                case PlotHits.Caption:/*
                    if (_kind == LogicKind.Not ||
                        _kind == LogicKind.Xor ||
                        _kind == LogicKind.FrontEdge ||
                        _kind == LogicKind.RsTrigger) return;
                    if (popup.Items.Count > 0)
                        popup.Items.Add(new ToolStripSeparator());
                    var item = new ToolStripMenuItem("Изменить функцию");
                    item.Click += (sender, args) => MessageBox.Show(@"Здесь пока ничего нет...", @"Сообщение разработчика");
                    popup.Items.Add(item);
                    //ChangeModuleMenu(item);*/
                    break;
            }
        }

        //private void ChangeModuleMenu(ToolStripDropDownItem item)
        //{
        //    item.DropDownItems.Add(new ToolStripMenuItem("\"OR\"")
        //        {
        //            BackColor = SystemColors.ActiveCaption,
        //            ForeColor = SystemColors.ActiveCaptionText
        //        });
        //    if (_kind != LogicKind.Or2)
        //        item.DropDownItems.Add(new ToolStripMenuItem("2x") {Tag = LogicKind.Or2});
        //    if (_kind != LogicKind.Or4 && _kind != LogicKind.Xor)
        //        item.DropDownItems.Add(new ToolStripMenuItem("4x") { Tag = LogicKind.Or4 });
        //    if (_kind != LogicKind.Or8 && _kind != LogicKind.Xor)
        //        item.DropDownItems.Add(new ToolStripMenuItem("8x") { Tag = LogicKind.Or8 });
        //    if (_kind != LogicKind.Xor &&
        //        (_kind == LogicKind.Or2 || _kind == LogicKind.And2))
        //    {
        //        item.DropDownItems.Add(new ToolStripSeparator());
        //        item.DropDownItems.Add(new ToolStripMenuItem("XOR") { Tag = LogicKind.Xor });
        //    }
        //    item.DropDownItems.Add(new ToolStripSeparator());
        //    item.DropDownItems.Add(new ToolStripMenuItem("\"AND\"")
        //        {
        //            BackColor = SystemColors.ActiveCaption,
        //            ForeColor = SystemColors.ActiveCaptionText
        //        });
        //    if (_kind != LogicKind.And2)
        //        item.DropDownItems.Add(new ToolStripMenuItem("2x") { Tag = LogicKind.And2 });
        //    if (_kind != LogicKind.And4 && _kind != LogicKind.Xor)
        //        item.DropDownItems.Add(new ToolStripMenuItem("4x") { Tag = LogicKind.And4 });
        //    if (_kind != LogicKind.And8 && _kind != LogicKind.Xor)
        //        item.DropDownItems.Add(new ToolStripMenuItem("8x") { Tag = LogicKind.And8 });
        //    foreach (var dropitem in item.DropDownItems.OfType<ToolStripMenuItem>())
        //        dropitem.Click += changeFunc_Click;
        //}

        void changeFunc_Click(object sender, EventArgs e)
        {
            var item = (ToolStripMenuItem) sender;
            var kind = (LogicKind) item.Tag;
            Kind = kind;
            Refresh();
        }

        private static Color GetOnColor()
        {
            return Color.LimeGreen;
        }

        private static Brush GetOnBrushColor()
        {
            return Brushes.LimeGreen;
        }

        private static string GetOnValue()
        {
            return "ON"; // "\"1\"";
        }

        private static Color GetOffColor()
        {
            return Color.Red;
        }

        private static Brush GetOffBrushColor()
        {
            return Brushes.Red;
        }

        private static string GetOffValue()
        {
            return "OFF"; // "\"0\"";
        }

        public override void DrawAt(Graphics g)
        {
            var rect = Bounds;
            if (rect.IsEmpty) return;
            g.FillRectangles(Selected ? SystemBrushes.ControlLight : SystemBrushes.Window, new[] { rect });
            using (var pen = new Pen(SystemColors.WindowText))
            {
                g.DrawRectangles(pen, new[] { rect });
                const float cirsize = BaseSize / 8;
                // имя модуля
                if (Name != null)
                {
                    var pt = rect.Location;
                    var gateInput = this as GateInput;
                    if (gateInput != null)
                    {
                        pt.X += rect.Width;
                        g.DrawString(Name, SystemFonts.MenuFont, SystemBrushes.WindowText, pt,
                                     new StringFormat
                                     {
                                         Alignment = StringAlignment.Far,
                                         LineAlignment = StringAlignment.Far
                                     });
                    }
                    else
                    {
                        var gateOutput = this as GateOutput;
                        if (gateOutput != null)
                        {
                            g.DrawString(Name, SystemFonts.MenuFont, SystemBrushes.WindowText, pt,
                                         new StringFormat
                                         {
                                             Alignment = StringAlignment.Near,
                                             LineAlignment = StringAlignment.Far
                                         });
                        }
                        else
                        {
                            pt.X += rect.Width/2;
                            g.DrawString(Name, SystemFonts.MenuFont, SystemBrushes.WindowText, pt,
                                         new StringFormat
                                             {
                                                 Alignment = StringAlignment.Center,
                                                 LineAlignment = StringAlignment.Far
                                             });

                        }
                    }
                }
                // имя функции
                var funcrect = new RectangleF(rect.Location, new SizeF(rect.Width, Height));
                funcrect.Inflate(-1, -3);
                funcrect.Offset(0, -3);
                using (var font = new Font("Arial Narrow", 10f, FontStyle.Bold))
                {
                    g.DrawString(FuncName(), font, SystemBrushes.WindowText, funcrect,
                                 new StringFormat
                                     {
                                         Alignment = StringAlignment.Center,
                                         LineAlignment = StringAlignment.Center
                                     });
                }
                // входы
                var pt1 = new PointF(rect.Location.X, rect.Location.Y + Height);
                var pt2 = new PointF(rect.Location.X - PinSize, rect.Location.Y + Height);
                for (uint i = 1; i <= InpCount; i++)
                {
                    var inputvalue = Inputs[i].Value;
                    var color = (inputvalue is bool) && Plots.Emulation
                                    ? (bool) inputvalue ? GetOnColor() : GetOffColor()
                                    : SystemColors.WindowText;
                    if (!Inputs[i].Invisible)
                        using (var inputpen = new Pen(color))
                        {
                            inputpen.Width = Inputs[i].Link != null && Inputs[i].Link.Module.Selected ? 2 : 1;
                            g.DrawLine(inputpen, pt1, pt2);
                        }
                    SizeF size;
                    // внешние ссылки
                    if (!Inputs[i].Invisible && Inputs[i].Link != null)
                    {
                        var source = Inputs[i].Link;
                        if (source.Module.PageNum != PageNum) // местные ссылки не рисуем
                        {
                            var slink = String.Format("Лист {0}, L{1}.{2}",
                                                      source.Module.PageNum, source.Module.OrderNum, source.Pin);
                            using (var font = new Font("Arial Narrow", 10f))
                            {
                                size = g.MeasureString(slink, font);
                                var pt = pt2;
                                pt.X -= size.Width;
                                g.FillRectangles(Plots.BackBrushColor, new[] {new RectangleF(pt, size)});
                                g.DrawRectangles(SystemPens.WindowText, new[] {new RectangleF(pt, size)});
                                g.DrawString(slink, font, SystemBrushes.WindowText, PointF.Add(pt, new SizeF(3, 0)));
                            }
                        }
                    }
                    // значение входа
                    if (!Inputs[i].Invisible && (Inputs[i].Link == null || Plots.Emulation))
                    {
                        var fp = CultureInfo.GetCultureInfo("en-US");
                        var isbool = Inputs[i].Value is bool;
                        var value = isbool
                                        ? String.Format(fp, "{0}",
                                                        (bool) Inputs[i].Value ? GetOnValue() : GetOffValue())
                                        : Inputs[i].Value is float
                                              ? String.Format(fp, "{0:F}", Inputs[i].Value)
                                              : String.Format(fp, "{0}", Inputs[i].Value);
                        var isboolOrEmu = isbool && Plots.Emulation;
                        using (
                            var font = isbool ? new Font("Arial Narrow", 8f, FontStyle.Bold) : new Font("Arial", 8f)
                            )
                        {
                            size = g.MeasureString(value, font);
                            var valuerect = new RectangleF(pt1, size);
                            valuerect.Offset(-size.Width - 1, -size.Height - 1);
                            var valuecolor = isboolOrEmu
                                                 ? ((bool) Inputs[i].Value ? GetOnBrushColor() : GetOffBrushColor())
                                                 : SystemBrushes.WindowText;
                            using (var brush = new SolidBrush(Color.FromArgb(200, Plots.BackColor)))
                                g.FillRectangles(brush, new[] {valuerect});
                            g.DrawString(value, font, valuecolor, valuerect,
                                         new StringFormat
                                             {
                                                 Alignment = StringAlignment.Center,
                                                 LineAlignment = StringAlignment.Center
                                             });
                        }
                    }
                    // инверсия входов
                    if (!Inputs[i].Invisible && Inputs[i].Invert)
                    {
                        var cr = new RectangleF(pt1, new SizeF(cirsize, cirsize));
                        cr.Offset(-cirsize/2, -cirsize/2);
                        g.FillEllipse(SystemBrushes.Window, cr);
                        inputvalue = Inputs[i].Value;
                        color = (inputvalue is bool) && Plots.Emulation
                                    ? !(bool) inputvalue ? GetOnColor() : GetOffColor()
                                    : SystemColors.WindowText;
                        using (var inputpen = new Pen(color))
                        {
                            g.DrawEllipse(inputpen, cr);
                        }
                    }
                    // наименование входа
                    if (!Inputs[i].Invisible && Inputs[i].Name != null)
                    {
                        var value = Inputs[i].Name;
                        using (var font = new Font("Arial Narrow", 7f))
                        {
                            size = g.MeasureString(value, font);
                            var namerect = new RectangleF(pt1, size);
                            namerect.Offset(1, -namerect.Height/2);
                            g.DrawString(value, font, SystemBrushes.WindowText, namerect,
                                         new StringFormat
                                             {
                                                 Alignment = StringAlignment.Center,
                                                 LineAlignment = StringAlignment.Center
                                             });
                        }
                    }
                    // смещение
                    pt1.Y += Height;
                    pt2.Y += Height;
                }
                // выходы
                pt1 = new PointF(rect.Location.X + rect.Width, rect.Location.Y + Height);
                pt2 = new PointF(rect.Location.X + rect.Width + PinSize,
                                 rect.Location.Y + Height);
                for (uint i = 1; i <= OutCount; i++)
                {
                    var outputvalue = Outputs[i].Value;
                    var color = (outputvalue is bool) && Plots.Emulation
                                    ? (bool) outputvalue ? GetOnColor() : GetOffColor()
                                    : SystemColors.WindowText;
                    using (var outputpen = new Pen(color))
                    {
                        outputpen.Width = Selected ? 2 : 1;
                        g.DrawLine(outputpen, PointF.Add(pt1, new SizeF(1, 0)), pt2);
                    }
                    SizeF size;
                    // внешние ссылки
                    if (Outputs[i].Link != null)
                    {
                        var pt = PointF.Add(pt2, new SizeF(5, 5));
                        var first = true;
                        foreach (var slink in from target in Outputs[i].Link.Targets
                                              where target.Module.PageNum != PageNum
                                              select String.Format("Лист {0}, L{1}.{2}",
                                                                   target.Module.PageNum, target.Module.OrderNum,
                                                                   target.Pin))
                        {
                            if (first)
                            {
                                g.DrawLine(SystemPens.ControlDarkDark, pt2.X, pt2.Y, pt2.X, pt.Y);
                                g.DrawLine(SystemPens.ControlDarkDark, pt2.X, pt.Y, pt.X, pt.Y);
                                first = false;
                            }
                            using (var font = new Font("Arial Narrow", 10f))
                            {
                                size = g.MeasureString(slink, font);
                                g.FillRectangles(Plots.BackBrushColor, new[] {new RectangleF(pt, size)});
                                g.DrawRectangles(SystemPens.ControlDarkDark, new[] {new RectangleF(pt, size)});
                                g.DrawString(slink, font, SystemBrushes.ControlDarkDark,
                                             PointF.Add(pt, new SizeF(3, 0)));
                            }
                            pt.Y += size.Height;
                        }
                    }
                    // значение выхода
                    if (this is GateInput || Plots.Emulation)
                    {
                        var fp = CultureInfo.GetCultureInfo("en-US");
                        var isbool = Outputs[i].Value is bool;
                        var value = isbool
                                        ? String.Format(fp, "{0}",
                                                        (bool) Outputs[i].Value ? GetOnValue() : GetOffValue())
                                        : Outputs[i].Value is float
                                              ? String.Format(fp, "{0:F}", Outputs[i].Value)
                                              : String.Format(fp, "{0}", Outputs[i].Value);

                        var isboolOrEmu = isbool && Plots.Emulation;
                        using (
                            var font = isbool ? new Font("Arial Narrow", 8f, FontStyle.Bold) : new Font("Arial", 8f)
                            )
                        {
                            size = g.MeasureString(value, font);
                            var valuerect = new RectangleF(pt1, size);
                            valuerect.Offset(1, -size.Height - 1);
                            var valuecolor = isboolOrEmu
                                                 ? ((bool) Outputs[i].Value ? GetOnBrushColor() : GetOffBrushColor())
                                                 : SystemBrushes.WindowText;
                            using (var brush = new SolidBrush(Color.FromArgb(200, Plots.BackColor)))
                                g.FillRectangles(brush, new[] {valuerect});
                            g.DrawString(value, font, valuecolor, valuerect,
                                         new StringFormat
                                             {
                                                 Alignment = StringAlignment.Center,
                                                 LineAlignment = StringAlignment.Center
                                             });
                        }
                    }
                    // инверсия выходов
                    if (Outputs[i].Invert)
                    {
                        var cr = new RectangleF(pt1, new SizeF(cirsize, cirsize));
                        cr.Offset(-cirsize/2, -cirsize/2);
                        g.FillEllipse(SystemBrushes.Window, cr);
                        g.DrawEllipse(SystemPens.WindowText, cr);
                    }
                    // наименование выхода
                    if (Outputs[i].Name != null)
                    {
                        var value = Outputs[i].Name;
                        using (var font = new Font("Arial Narrow", 7f))
                        {
                            size = g.MeasureString(value, font);
                            var namerect = new RectangleF(pt1, size);
                            namerect.Offset(-namerect.Width + 1, -namerect.Height/2);
                            g.DrawString(value, font, SystemBrushes.WindowText, namerect,
                                         new StringFormat
                                             {
                                                 Alignment = StringAlignment.Center,
                                                 LineAlignment = StringAlignment.Center
                                             });
                        }
                    }
                    // смещение
                    pt1.Y += Height;
                    pt2.Y += Height;
                }
                // номер модуля
                var orderrect = new RectangleF(rect.Location, new SizeF(rect.Width, Height));
                orderrect.Offset(0, rect.Height - Height + 3);
                orderrect.Inflate(-1, -3);
                g.DrawString(String.Format("L{0}", OrderNum), SystemFonts.MenuFont, SystemBrushes.WindowText, orderrect,
                    new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            }
        }

        public override void DrawOutputLinks(Graphics g)
        {
            for (uint i = 1; i <= Outputs.Length; i++)
            {
                if (Outputs[i].Link == null) continue;
                var pathlines = new GraphicsPath();
                var value = Outputs[i].Value;
                var color = (value is bool) && Plots.Emulation
                                ? (bool)value ? GetOnColor() : GetOffColor()
                                : SystemColors.WindowText;
                BuildOutputLinks(i, pathlines);
                // очистка пути толстой линией цвета фона
                using (var pen = new Pen(Plots.BackColor))
                {
                    pen.Width = 7;
                    g.DrawPath(pen, pathlines);
                }

                // рисование пути цветом выхода
                using (var pen = new Pen(color))
                {
                    pen.Width = Selected ? 2 : 1;
                    g.DrawPath(pen, pathlines);
                }
            }
        }

        private void BuildOutputLinks(uint output, GraphicsPath pathlines,
            EdgeKind edge = EdgeKind.None)
        {
            for (var index = 0; index < Outputs[output].Link.Targets.Count; index++)
                BuildOutputOneLink(output, index, pathlines, edge);
        }

        private void BuildOutputOneLink(uint output, int index, GraphicsPath pathlines, EdgeKind edge = EdgeKind.None)
        {
            var pt1 = GetOutputPinLocation(output);
            var pt0 = pt1;
            var target = Outputs[output].Link.Targets[index];
            if (target.PageOrder != PageNum) return;
            pt1.X += target.FirstPoint.X;
            pt1.Y += target.FirstPoint.Y;
            if (edge == EdgeKind.None)
            {
                pathlines.StartFigure();
                pathlines.AddLine(pt0, pt1);
                pathlines.CloseFigure();
            }
            var pt2 = target.Module.GetInputPinLocation(target.Pin);
            if (pt2.X < pt1.X)
            {
                var pt3 = pt2;
                if (pt2.Y < pt1.Y)
                {
                    #region через низ
                    var inpcount = target.Module.InpCount;
                    pt3.X = target.Module.Location.X - (BaseSize / 4) * (inpcount - target.Pin) - BaseSize / 2;
                    pt3.Y = target.Module.Location.Y + (BaseSize / 4) * (inpcount - target.Pin) + BaseSize / 2 +
                            target.Module.BoundsRect.Height;
                    #endregion через верх
                }
                else
                {
                    #region через верх
                    pt3.X = target.Module.Location.X - (BaseSize / 4) * target.Pin - BaseSize / 2;
                    pt3.Y = target.Module.Location.Y - (BaseSize / 4) * target.Pin - BaseSize / 4;
                    #endregion через верх
                }
                AddLinesBetweenPoints(pathlines, pt1, pt3, edge);
                AddLinesBetweenPoints(pathlines, pt3, pt2, edge);
            }
            else
                AddLinesBetweenPoints(pathlines, pt1, pt2, edge);
        }

        private static void AddLinesBetweenPoints(GraphicsPath pathlines, 
            PointF pt1, PointF pt2, EdgeKind edge = EdgeKind.None)
        {
            var x1 = Math.Min(pt1.X, pt2.X);
            var x2 = Math.Max(pt1.X, pt2.X);
            var y1 = Math.Min(pt1.Y, pt2.Y);
            var y2 = Math.Max(pt1.Y, pt2.Y);
            var width = Math.Abs(x2 - x1);
            var height = Math.Abs(y2 - y1);
            const float epsilon = 0.0001f;
            if (Math.Abs(width - 0) < epsilon ||
                Math.Abs(height - 0) < epsilon)
            {
                // одиночная вертикальная или горизонтальная линия
                if (edge == EdgeKind.None ||
                    (Math.Abs(width - 0) < epsilon && edge == EdgeKind.Left))
                {
                    pathlines.StartFigure();
                    pathlines.AddLine(pt1, pt2);
                    pathlines.CloseFigure();
                }
            }
            else
            {
                var pt = pt1;
                if (pt1.Y < pt2.Y)
                    pt.Y += height;
                else
                    pt.Y -= height;
                if (edge == EdgeKind.None || edge == EdgeKind.Left)
                {
                    // вертикальная линия
                    pathlines.StartFigure();
                    pathlines.AddLine(pt1, pt);
                    pathlines.CloseFigure();
                }
                if (edge == EdgeKind.None)
                {
                    // горизонтальная линия
                    pathlines.StartFigure();
                    pathlines.AddLine(pt, pt2);
                    pathlines.CloseFigure();
                }
            }
        }

        override public HitInfo CheckMouseHitAt(PointF point)
        {
            for (uint output = 1; output <= Outputs.Length; output++)
            {
                if (Outputs[output].Link == null) continue;
                for (var index = Outputs[output].Link.Targets.Count - 1; index >= 0; index--)
                {
                    using (var pathlines = new GraphicsPath())
                    {
                        BuildOutputOneLink(output, index, pathlines, EdgeKind.Left);
                        if (pathlines.PointCount < 2) continue;
                        var pt1 = pathlines.PathPoints[0];
                        var pt2 = pathlines.PathPoints[1];
                        var x = pt1.X - 3;
                        var y = Math.Min(pt1.Y, pt2.Y);
                        const float w = 7f;
                        var h = Math.Abs(pt1.Y - pt2.Y);
                        var rect = new RectangleF(x, y, w, h);
                        if (rect.Contains(point))
                            return new HitInfo {Hits = PlotHits.LeftEdge, InOut = output, LinkIndex = index};
                    }
                }
            }
            return base.CheckMouseHitAt(point);
        }

    }
}
