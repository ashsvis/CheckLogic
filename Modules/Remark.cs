using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace CheckLogic
{
    [Serializable]
    public class Remark : Plot
    {
        public Remark() { }

        public readonly List<string> Lines = new List<string>();

        override public void SaveProperties(NameValueCollection coll)
        {
            base.SaveProperties(coll);
            var fp = CultureInfo.GetCultureInfo("en-US");
            coll["Kind"] = String.Format(fp, "Text.Remark");
            var n = 1;
            foreach (var line in Lines)
                coll["Line" + (n++)] = line;
        }

        public override void LoadProperties(NameValueCollection coll)
        {
            base.LoadProperties(coll);
            // далее загрузка свойств для типа Logic
            var akind = (coll["Kind"] ?? "").Split(new[] { '.' });
            if (akind.Length != 2 || akind[0] != "Text") return;
            if (akind[1] == "Remark")
            {
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
            }
        }

        public Remark(PlotsOwner owner)
            : base(owner)
        {
            Size = new SizeF(BaseSize, BaseSize / 2);
            Lines.Add("Комментарий");
        }

        override public object Clone()
        {
            var remark = new Remark(Plots);
            var coll = new NameValueCollection();
            SaveProperties(coll);
            remark.LoadProperties(coll);
            return remark;
        }

        override public PlotHits Contains(PointF point)
        {
            var hits = base.CheckMouseHitAt(point).Hits;
            return hits == PlotHits.InputLink || hits == PlotHits.OutputLink ? PlotHits.None : hits;
        }

        override public string ModuleName() // отображаемое наименование модуля
        {
            return Name ?? String.Format("R{0}", OrderNum);
        }

        override public void DrawAt(Graphics g) // рисование элемента
        {
            var rect = Bounds;
            if (rect.IsEmpty) return;
            if (Lines.Count == 0)
            {
                using (var pen = new Pen(SystemColors.WindowText))
                {
                    //pen.Width = Selected ? 2 : 1;
                    pen.DashStyle = DashStyle.Dash;
                    if (Selected) g.FillRectangles(SystemBrushes.ActiveCaption, new[] { rect });
                    g.DrawRectangles(pen, new[] { rect });
                }
            }
            if (Selected)
                g.FillRectangles(SystemBrushes.ControlLight, new[] {rect});
            var pt = rect.Location;
            pt.Y -= 3;
            pt.X += 1;
            var width = 0f;
            foreach (var line in Lines)
            {
                var size = g.MeasureString(line, SystemFonts.MenuFont);
                if (width < size.Width) width = size.Width;
                g.DrawString(line, SystemFonts.MenuFont, SystemBrushes.WindowText, pt,
                    new StringFormat { Alignment = StringAlignment.Near, 
                        LineAlignment = StringAlignment.Near });
                pt.Y += BaseSize / 4;
            }
            Size = new SizeF(width, Lines.Count > 0 ? (BaseSize / 4) * Lines.Count : BaseSize / 2);
            rect = Bounds;
            if (rect.IsEmpty) Size = new SizeF(BaseSize, BaseSize / 2);
        }

    }
}
