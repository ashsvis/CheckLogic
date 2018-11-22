using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace CheckLogic
{
    public enum PlotHits
    {
        None,
        Body,
        Caption,
        OrderNum,
        InputLink,
        OutputLink,
        Descriptor,
        LeftEdge
    }

    public struct HitInfo
    {
        public PlotHits Hits;
        public uint InOut;
        public int LinkIndex;
    }

    public delegate List<Plot> GetAllPlots();

    public class ChangeEventArgs: EventArgs
    {
        public bool Changed { get; set; }
    }

    [Serializable]
    public class Plot : ICloneable
    {
        public Plot() { }

        protected PlotsOwner Plots;

        public void SetPlots(PlotsOwner list)
        {
            Plots = list;
        }

        protected Plot(PlotsOwner owner)
        {
            Plots = owner;
        }

        virtual public void Calculate() { /* заглушка */ }

        protected const float BaseSize = 48f;
        public const float Height = BaseSize / 2;
        protected const float PinSize = BaseSize / 2;

        [NonSerialized] 
        protected readonly ContextMenuStrip PopupMenu = new ContextMenuStrip();

        #region сохраняемые свойства

        public uint PageNum { get; set; } // порядковый номер страницы 

        public uint OrderNum { get; set; } // порядковый номер элемента для расчёта и отображения

        public uint OlderOrderNum; // старый порядковый номер элемента для расчёта и отображения

        virtual public uint InpCount // количество входов
        {
            get { return Inputs.Length; }
            protected set
            {
                Inputs.Length = value;
                Calculate();
            }
        }

        virtual protected uint OutCount // количество выходов
        {
            get { return Outputs.Length; }
            set
            {
                Outputs.Length = value;
                Calculate();
            }
        }

        public PointF Location { get; set; }

        public string Name { get; set; }

        public readonly InputCollection Inputs = new InputCollection();  // содержит текущие значения входов (до входной инверсии)

        #endregion сохраняемые свойства
        
        virtual public void SaveProperties(NameValueCollection coll)
        {
            coll.Clear();
            var fp = CultureInfo.GetCultureInfo("en-US");
            coll["OrderNum"] = String.Format(fp, "{0}", OrderNum);
            coll["Location"] = String.Format(fp, "{0},{1}", Location.X, Location.Y);
        }

        virtual public void LoadProperties(NameValueCollection coll)
        {
            var fp = CultureInfo.GetCultureInfo("en-US");
            uint n;
            if (uint.TryParse(coll["OrderNum"] ?? "", NumberStyles.Any, fp, out n))
                OrderNum = n;
            var location = (coll["Location"] ?? "").Split(new [] { ',' });
            if (location.Length != 2) return;
            float x, y;
            if (float.TryParse(location[0], NumberStyles.Any, fp, out x) &&
                float.TryParse(location[1], NumberStyles.Any, fp, out y))
                Location = new PointF(x, y);
        }

        public readonly OutputCollection Outputs = new OutputCollection(); // содержит текущие посчитанные значения выходов (после выходной инверсии)

        public readonly List<LinkInfo> OutputLinks = new List<LinkInfo>();
        public readonly List<string> GateLinks = new List<string>();

        public event EventHandler OnInvalidate;
        public event EventHandler OnRemove;

        protected void Remove()
        {
            if (OnRemove != null) OnRemove(this, new EventArgs());
        }

        public bool Selected { get; set; }

        protected void Refresh(bool change = false) // отправляет запрос на перерисовку всего поля элеменов
        {
            if (OnInvalidate != null) OnInvalidate(this, new ChangeEventArgs { Changed = change });            
        }

        public void AddTargetLink(uint output, ModulePin targetpin)
        {
            if (Outputs[output].Link == null)
                Outputs[output].Link = new LinkPin(targetpin);
            else
                Outputs[output].Link.Targets.Add(targetpin);
            targetpin.Module.AddSourceLink(targetpin.Pin, 
                new ModulePin {Module = this, Pin = output});
        }

        private void AddSourceLink(uint input, ModulePin source)
        {
            if (Inputs[input].Link != null) // удаление входной связи у предыдущего модуля
                Inputs[input].Link.Module.RemoveSourceLink(Inputs[input].Link.Pin);
            Inputs[input].Link = source;
        }

        /// <summary>
        /// Удаление ссылок на этот модуль у модулей-источников
        /// </summary>
        /// <param name="input">номер входного пина, содержащего ссылку на модуль-источник и его выход</param>
        public void RemoveSourceLink(uint input)
        {
            var source = Inputs[input].Link;
            if (source == null) return;
            if (source.Module.Outputs[source.Pin].Link == null) return;
            source.Module.Outputs[source.Pin].Link.Targets.RemoveAll(module => 
                module.Module == this && module.Pin == input);
            if (source.Module.Outputs[source.Pin].Link.Targets.Count == 0)
                source.Module.Outputs[source.Pin].Link = null;
            Inputs[input].Link = null;
        }

        /// <summary>
        /// Удаление ссылок в этом модуле на целевые модули
        /// </summary>
        public void RemoveTargetLinkFor(Plot module)
        {
            var list = new List<uint>();
            for (uint i = 1; i <= OutCount; i++)
            {
                var link = Outputs[i].Link;
                if (link == null) continue;
                for (var n = link.Targets.Count - 1; n >= 0; n--)
                {
                    if (n >= link.Targets.Count) continue;
                    var target = link.Targets[n];
                    target.Module.RemoveSourceLink(target.Pin);
                }
                link.Targets.RemoveAll(item => item.Module == module);
                if (link.Targets.Count == 0) list.Add(i);
            }
            foreach (var i in list)
                Outputs[i].Link = null;
        }

        protected SizeF Size { private get; set; }

        protected RectangleF Bounds
        {
            get
            {
                return new RectangleF(Location, Size);
            }
        }

        virtual public RectangleF BoundsRect
        {
            get
            {
                var rect = Bounds;
                return rect;
            }
        }

        virtual protected string FuncName() // отображаемое наименование функции
        {
            return "(undef)"; // заглушка
        }

        virtual public string ModuleName() // отображаемое наименование модуля
        {
            return Name ?? String.Format("L{0}", OrderNum); 
        }

        public override string ToString()
        {
            return ModuleName();
        }

        public PointF GetInputPinLocation(uint input)
        {
            var rect = Bounds;
            if (!rect.IsEmpty)
            {
                var pt2 = new PointF(rect.Location.X - PinSize,
                                     rect.Location.Y + Height);
                for (var i = 1; i <= InpCount; i++)
                {
                    if (i == input) return pt2;
                    // смещение
                    pt2.Y += Height;
                }
            }
            return new PointF();
        }

        protected PointF GetOutputPinLocation(uint output)
        {
            var rect = Bounds;
            if (!rect.IsEmpty)
            {
                var pt2 = new PointF(rect.Location.X + rect.Width + PinSize,
                                     rect.Location.Y + Height);
                for (var i = 1; i <= OutCount; i++)
                {
                    if (i == output) return pt2;
                    // смещение
                    pt2.Y += Height;
                }
            }
            return new PointF();
        }

        virtual public void DrawAt(Graphics g) // рисование элемента
        {
            var rect = Bounds;
            if (rect.IsEmpty) return;
            g.FillRectangles(SystemBrushes.Window, new[] { rect });
            g.DrawRectangles(SystemPens.WindowText, new[] { rect });
        }

        virtual public void DrawOutputLinks(Graphics g)
        {
            // заглушка
        }

        virtual protected bool InvertEnabled(PlotHits hits, uint inOut)
        {
            return true; // заглушка
        }

        virtual protected void AddPopupItems(ContextMenuStrip popup, HitInfo hitinfo)
        {
            // заглушка
        }

        virtual public bool ClickAt(PointF point)
        {
            return false; // заглушка
        }

        virtual public void ShowPopupAt(Control parent, PointF point)
        {
            /* заглушка
             */
        }

        /// <summary>
        /// Проверка нажатия на модуле
        /// </summary>
        /// <param name="point">точка нажатия</param>
        /// <returns>Возвращает информацию об области нажатия</returns>
        virtual public PlotHits Contains(PointF point)
        {
            return CheckMouseHitAt(point).Hits;
        }

        virtual protected bool HitEnabled(PlotHits hits, uint inOut = 0)
        {
            return true; // заглушка
        }

        /// <summary>
        /// Проверка нажатия на модуле
        /// </summary>
        /// <param name="point">точка нажатия</param>
        /// <returns>Возвращает информацию об области нажатия (и номер входа или выхода)</returns>
        virtual public HitInfo CheckMouseHitAt(PointF point)
        {
            var rect = Bounds;
            if (rect.IsEmpty)
                return new HitInfo { Hits = PlotHits.None };
            // имя функции
            var funcrect = new RectangleF(rect.Location, new SizeF(rect.Width, Height));
            funcrect.Inflate(-3, -3);
            if (funcrect.Contains(point) && HitEnabled(PlotHits.Caption))
                return new HitInfo { Hits = PlotHits.Caption };
            // номер модуля
            var orderrect = new RectangleF(rect.Location, new SizeF(rect.Width, Height));
            orderrect.Offset(0, rect.Height - Height);
            orderrect.Inflate(-3, -3);
            if (orderrect.Contains(point) && HitEnabled(PlotHits.OrderNum))
                return new HitInfo { Hits = PlotHits.OrderNum };
            // тело модуля
            if (rect.Contains(point) && HitEnabled(PlotHits.Body))
                return new HitInfo { Hits = PlotHits.Body };
            // входы
            var pt1 = new PointF(rect.Location.X, rect.Location.Y + Height);
            var pt2 = new PointF(rect.Location.X - PinSize,
                                 rect.Location.Y + Height);
            for (var i = 1; i <= InpCount; i++)
            {
                // места для щелчка мышки
                var rp = new RectangleF(pt1, new SizeF(PinSize, Height));
                rp.Offset(-PinSize, -Height * 0.9f);
                if (rp.Contains(point) && HitEnabled(PlotHits.InputLink, (uint)i))
                    return new HitInfo { Hits = PlotHits.InputLink, InOut = (uint)i };
                // смещение
                pt1.Y += Height;
                pt2.Y += Height;
            }
            // выходы
            pt1 = new PointF(rect.Location.X + rect.Width, rect.Location.Y + Height);
            pt2 = new PointF(rect.Location.X + rect.Width + PinSize,
                             rect.Location.Y + Height);
            for (var i = 1; i <= OutCount; i++)
            {
                // места для щелчка мышки
                var rp = new RectangleF(pt1, new SizeF(PinSize, Height));
                rp.Offset(0, -Height * 0.9f);
                if (rp.Contains(point) && HitEnabled(PlotHits.OutputLink, (uint)i))
                    return new HitInfo { Hits = PlotHits.OutputLink, InOut = (uint)i };
                // смещение
                pt1.Y += Height;
                pt2.Y += Height;
            }
            return new HitInfo { Hits = PlotHits.None };
        }

        virtual public object Clone()
        {
            // заглушка
            var plot = new Plot(Plots);
            var coll = new NameValueCollection();
            SaveProperties(coll);
            plot.LoadProperties(coll);
            return plot;
        }
    }

    /*
    public class EditNameEventArgs : EventArgs
    {
        public string Value { get; set; }
    }

    public delegate void EditNameEventHandler(Plot sender, EditNameEventArgs e);

    public class EditOrderEventArgs : EventArgs
    {
        public uint Value { get; set; }
    }

    public delegate void EditOrderEventHandler(Plot sender, EditOrderEventArgs e);

    public class EditValueEventArgs : EventArgs
    {
        public float Value { get; set; }
        public HitInfo HitInfo { get; set; }
    }

    public delegate void EditValueEventHandler(Plot sender, EditValueEventArgs e);
    */

    public class DragedOutputInfo
    {
        public Plot Module { get; set; }
        public HitInfo HitInfo { get; set; }
    }

    public class LinkInfo
    {
        public string OutputLinks { get; set; }
        public string FirstPoints { get; set; }
        public string LastPoints { get; set; }
    }
}
