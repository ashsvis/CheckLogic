using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace CheckLogic
{
    public partial class ChildForm : Form, IStopFetch
    {
        private readonly PlotsOwner _plotlist;
        private readonly PlotsOwner _textlist;

        private readonly GetAllPlots _getAllPlots;

        private readonly bool _testing;
        private readonly IDictionary<string, Type> _plugins;

        public bool Emulation { get { return _testing; } }

        private readonly Color _backcolor;
        private readonly Brush _backbrushcolor;

        private readonly BackgroundWorker _clockWorker = new BackgroundWorker
        {
            WorkerSupportsCancellation = true,
            WorkerReportsProgress = true
        };

        private int _timescount;

        public void Done()
        {
            ClockRun(false);
        }

        private void ClockRun(bool run)
        {
            if (run && !_clockWorker.IsBusy)
            {
                _clockWorker.DoWork += (sender, args) =>
                    {
                        var worker = (BackgroundWorker)sender;
                        while (!worker.CancellationPending)
                        {
                            Thread.Sleep(1);
                            var now = DateTime.Now;
                            if (_msecond == now.Millisecond) continue;
                            _msecond = now.Second;
                            _timescount++;
                            if (_timescount < 1) continue;
                            _timescount = 0;
                            worker.ReportProgress(0);
                        }
                    };
                _clockWorker.ProgressChanged += ClockWorker_ProgressChanged;
                _clockWorker.RunWorkerAsync();
            }
            else if (_clockWorker.IsBusy)
            {
                _clockWorker.CancelAsync();
            }
        }

        private void ClockWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (_reloading) return;
            foreach (var plot in _plotlist) plot.Calculate();
            PictBox.Invalidate();
        }

        public ChildForm(GetAllPlots allPlots, bool testing, IDictionary<string, Type> plugins)
        {
            InitializeComponent();
            _testing = testing;
            _plugins = plugins;
            _backcolor = _testing ? SystemColors.Control : SystemColors.Window;
            _backbrushcolor = _testing ? SystemBrushes.Control : SystemBrushes.Window;
            PictBox.BackColor = _backcolor;
            PictBox.Size = new Size(3000, 3000);
            _getAllPlots = allPlots;
            _plotlist = new PlotsOwner(_getAllPlots) { Emulation = _testing, BackColor = _backcolor, BackBrushColor = _backbrushcolor };
            _textlist = new PlotsOwner(_getAllPlots) { Emulation = _testing, BackColor = _backcolor, BackBrushColor = _backbrushcolor };
            if (_testing)
                ClockRun(true);
        }

        public IEnumerable<Plot> PlotList { get { return _plotlist; } } 

        public uint PageNum { get; set; }

        private void ChildForm_Load(object sender, EventArgs e)
        {
        }

        public bool HasSelected()
        {
            return _plotlist.Any(plt => plt.Selected) ||
                   _textlist.Any(plt => plt.Selected);
        }

        public void CopySelectedToClipboard()
        {
            var forcopy = new List<Plot>();
            forcopy.AddRange(_plotlist.Where(plt=>plt.Selected));
            forcopy.AddRange(_textlist.Where(plt => plt.Selected));
            if (forcopy.Count == 0) return;
            var frm = MdiParent as IClipboardSupport;
            if (frm == null) return;
            frm.Copy(forcopy);
        }

        public void CutSelectedToClipboard()
        {
            var forcopy = new List<Plot>();
            forcopy.AddRange(_plotlist.Where(plt => plt.Selected));
            forcopy.AddRange(_textlist.Where(plt => plt.Selected));
            if (forcopy.Count == 0) return;
            var frm = MdiParent as IClipboardSupport;
            if (frm == null) return;
            frm.Cut(forcopy);
            _plotlist.RemoveAll(plt => plt.Selected);
            _textlist.RemoveAll(plt => plt.Selected);
            _selrect = new Rectangle();
            Changed(true);
        }

        public void PasteFromClipboardAndSelected()
        {
            var frm = MdiParent as IClipboardSupport;
            if (frm == null) return;
            var list = frm.Paste() as List<Plot>;
            if (list != null && list.Any())
            {
                UnselectedPlots();
                UnselectedRemarks();
                var forlink = new List<Plot>();
                var n = frm.PastesCount();
                foreach (var plt in list)
                {
                    var offset = PointF.Add(plt.Location, new Size(ToGrid(15 * n), ToGrid(15 * n)));
                    var coll = new NameValueCollection();
                    plt.SaveProperties(coll);
                    var newplot = (Plot)plt.Clone();
                    newplot.OlderOrderNum = plt.OrderNum;
                    newplot.LoadProperties(coll);
                    newplot.PageNum = PageNum;
                    newplot.Location = offset;
                    newplot.Selected = true;
                    if (plt is Remark)
                    {
                        _textlist.Add(newplot);
                        newplot.OrderNum = (uint)_textlist.Count;
                    }
                    else
                    {
                        _plotlist.Add(newplot);
                        newplot.OrderNum = (uint)_plotlist.Count;
                        ConnectLogicEvents(newplot);
                        forlink.Add(newplot);
                    }
                    BuildUnionSelRect(newplot);
                }
                ConnectLinks(forlink);
                PictBox.Invalidate();
            }
            Changed(true);
        }

        private void Changed(bool value)
        {
            var frm = MdiParent as IChanged;
            if (frm == null) return;
            frm.Changed(value);
        }

        public string PageName { get; set; }

        public void LoadContent(MemIniFile mif)
        {
            var list = new List<Plot>();
            var remarks = new List<Plot>();
            var pagename = PageName;
            var sectnames = new List<string>(mif.ReadSections());
            sectnames.Sort();
            foreach (var section in sectnames)
            {
                var thispage = section.StartsWith(pagename + ".");
                if (!thispage) continue;
                var coll = mif.ReadSectionNamesAndValues(section);
                var classtype = (coll["Kind"] ?? "").Split(new[] {'.'})[0];
                Plot module;
                Point pt;
                if (classtype == "Text")
                {
                    module = new Remark(_textlist);
                    module.LoadProperties(coll);
                    pt = Point.Ceiling(module.Location);
                    module.Location = new PointF(ToGrid(pt.X), ToGrid(pt.Y));
                    remarks.Add(module);                    
                }
                else if (_plugins.ContainsKey(classtype))
                {
                    module = (Plot) Activator.CreateInstance(_plugins[classtype]);
                    module.SetPlots(_plotlist);
                    module.PageNum = PageNum;
                    module.LoadProperties(coll);
                    module.Name = coll["Name"];
                    pt = Point.Ceiling(module.Location);
                    module.Location = new PointF(ToGrid(pt.X), ToGrid(pt.Y));
                    ConnectLogicEvents(module as Logic);
                    list.Add(module);
                }
            }
            //-----------------------------
            var query = from module in list
                        orderby module.OrderNum
                        select module;
            var remarksquery = from module in remarks
                        orderby module.OrderNum
                        select module;
            _reloading = true;
            try
            {
                _textlist.Clear();
                _textlist.AddRange(remarksquery);
                _plotlist.Clear();
                _plotlist.AddRange(query);
                ConnectLinks(_plotlist);
                PictBox.Invalidate();
            }
            finally
            {
                _reloading = false;
            }
        }

        public readonly List<string> ExternalOutputLinks = new List<string>();
        public readonly List<string> ExternalGateLinks = new List<string>();

        private void ConnectLinks(List<Plot> list) 
        {
            ExternalOutputLinks.Clear();
            var fp = CultureInfo.GetCultureInfo("en-US");
            foreach (var plot in list)
            {
                var n = (uint) 1;
                foreach (var link in plot.OutputLinks)
                {
                    var alink = link.OutputLinks.Split(new[] {','});
                    if (alink.Length == 0) continue;
                    var pfirst = new PointF[alink.Length];
                    var afirst = (link.FirstPoints ?? "").Split(new[] {','});
                    string[] apoint;
                    for (var i = 0; i < afirst.Length; i++)
                        if (i < pfirst.Length)
                        {
                            apoint = afirst[i].Split(new[] {':'});
                            if (apoint.Length == 2)
                            {
                                float x, y;
                                if (float.TryParse(apoint[0], NumberStyles.Float, fp, out x) &&
                                    float.TryParse(apoint[1], NumberStyles.Float, fp, out y))
                                    pfirst[i] = new PointF(x, y);
                            }
                        }
                    var plast = new PointF[alink.Length];
                    var alast = (link.LastPoints ?? "").Split(new[] {','});
                    for (var i = 0; i < alast.Length; i++)
                        if (i < plast.Length)
                        {
                            apoint = alast[i].Split(new[] {':'});
                            if (apoint.Length == 2)
                            {
                                float x, y;
                                if (float.TryParse(apoint[0], NumberStyles.Float, fp, out x) &&
                                    float.TryParse(apoint[1], NumberStyles.Float, fp, out y))
                                    plast[i] = new PointF(x, y);
                            }
                        }
                    var k = 0;
                    foreach (var module in alink)
                    {
                        var amodule = module.Split(new[] {'.'});
                        if (amodule.Length == 3)
                        {
                            uint page = 0, ordernum, pin;
                            if ((amodule[0] == "*" || uint.TryParse(amodule[0], out page)) &&
                                uint.TryParse(amodule[1], out ordernum) &&
                                uint.TryParse(amodule[2], out pin))
                            {
                                if (amodule[0] == "*" || page == PageNum) // своя страница
                                {
                                    var target = list.FirstOrDefault(m => m.OrderNum == ordernum +
                                                                          (m.OlderOrderNum > 0
                                                                               ? (m.OrderNum - m.OlderOrderNum)
                                                                               : (uint) 0));
                                    if (target != null && pin <= target.Inputs.Length)
                                    {
                                        plot.AddTargetLink(n,
                                                           new ModulePin
                                                               {
                                                                   Module = target,
                                                                   Pin = pin,
                                                                   PageOrder = page > 0 ? page : PageNum,
                                                                   FirstPoint = pfirst[k],
                                                                   LastPoint = plast[k]
                                                               });
                                    }
                                }
                                else
                                    ExternalOutputLinks.Add(String.Format("{0}.{1}.{2}.{3}", PageNum, plot.OrderNum, n,
                                                                          module));
                            }
                        }
                        k++;
                    }
                    n++;
                }
                //
                var gateoutput = plot as GateOutput;
                if (gateoutput != null)
                foreach (var module in gateoutput.GateLinks)
                {
                    var amodule = module.Split(new[] {'.'});
                    if (amodule.Length == 3)
                    {
                        uint page = 0, ordernum, pin;
                        if ((amodule[0] == "*" || uint.TryParse(amodule[0], out page)) &&
                            uint.TryParse(amodule[1], out ordernum) &&
                            uint.TryParse(amodule[2], out pin))
                        {
                            if (amodule[0] == "*" || page == PageNum) // своя страница
                            {
                                var gateinput = list.FirstOrDefault(m => m.OrderNum == ordernum +
                                                                      (m.OlderOrderNum > 0
                                                                           ? (m.OrderNum - m.OlderOrderNum)
                                                                           : (uint) 0)) as GateInput;
                                if (gateinput != null && pin <= gateinput.Inputs.Length)
                                    gateoutput.AddGateInput(gateinput);
                            }
                            else
                                ExternalGateLinks.Add(String.Format("{0}.{1}.{2}.{3}", PageNum, gateoutput.OrderNum, n,
                                                                          module));

                        }
                    }
                }
            }
        }

        public Plot GetModuleByOrderNum(uint pagenum, uint ordernum)
        {
            return _plotlist.FirstOrDefault(m => m.PageNum == pagenum && m.OrderNum == ordernum);
        }

        public void SaveContent(MemIniFile mif)
        {
            _reloading = true;
            try
            {
                var pagename = PageName;
                var list = new List<Plot>(_plotlist);
                list.AddRange(_textlist);
                foreach (var plot in list)
                {
                    var values = new NameValueCollection();
                    plot.SaveProperties(values);
                    var sectname = String.Format("{0}.{1}", pagename, plot.ModuleName());
                    foreach (var key in values.AllKeys)
                        mif.WriteString(sectname, key, values[key]);
                    if (!string.IsNullOrWhiteSpace(plot.Name)) mif.WriteString(sectname, "Name", plot.Name);
                }
            }
            finally
            {
                _reloading = false;
            }
        }

        private void PictBox_Paint(object sender, PaintEventArgs e)
        {
            // прорисовка связей между модулями
            foreach (var plot in _plotlist) // для всех модулей на схеме
                plot.DrawOutputLinks(e.Graphics);
            // прорисовка комментариев
            foreach (var plot in _textlist)
                plot.DrawAt(e.Graphics);
            // прорисовка модулей
            foreach (var plot in _plotlist)
                plot.DrawAt(e.Graphics);
            // прямоугольник для перемещения модуля
            if (!_selrect.IsEmpty)
                using (var pen = new Pen(SystemColors.WindowText))
                {
                    pen.DashStyle = DashStyle.Dot;
                    var rect = _selrect;
                    e.Graphics.DrawRectangle(pen, rect);
                }
            // для выбора модулей прямоугольником
            if (_selByRectMode)
                using (var pen = new Pen(SystemColors.WindowText))
                {
                    pen.DashStyle = DashStyle.Dot;
                    var rect = CalcSelByRect();
                    e.Graphics.DrawRectangle(pen, rect);
                }
        }

        private Rectangle CalcSelByRect()
        {
            var left = Math.Min(_startselpoint.X, _endselpoint.X);
            var top = Math.Min(_startselpoint.Y, _endselpoint.Y);
            var width = Math.Abs(_startselpoint.X - _endselpoint.X);
            var height = Math.Abs(_startselpoint.Y - _endselpoint.Y);
            //Console.WriteLine("X:{0} Y:{1} W:{2} H:{3}", left, top, width, height);
            return new Rectangle(left, top, width, height);
        }

        private Plot GetPlotByBody(Point location)
        {
            var list = new List<Plot>(_textlist);
            list.AddRange(_plotlist);
            list.Reverse();
            return (from plot in list 
                    let found = plot.Contains(location) 
                    where found == PlotHits.Body || 
                        found == PlotHits.Caption || 
                        found == PlotHits.OrderNum ||
                        found == PlotHits.Descriptor
                    select plot).FirstOrDefault();
        }

        private Plot GetPlotByAll(Point location)
        {
            var list = new List<Plot>(_textlist);
            list.AddRange(_plotlist);
            list.Reverse();
            return list.FirstOrDefault(plot => 
                plot.Contains(location) != PlotHits.None);
        }

        private Rectangle _selrect;

        private void BuildUnionSelRect(Plot plt)
        {
            _selrect = Rectangle.Ceiling(plt.BoundsRect);
            foreach (var item in _plotlist.Where(plot => plot.Selected))
                _selrect = Rectangle.Union(_selrect,
                    Rectangle.Ceiling(item.BoundsRect));
            foreach (var item in _textlist.Where(plot => plot.Selected))
                _selrect = Rectangle.Union(_selrect,
                    Rectangle.Ceiling(item.BoundsRect));
        }

        private Point _selpoint, _startselpoint, _endselpoint;
        private Point _selrectpoint;

        private static int ToGrid(int value)
        {
            const float dbf = Plot.Height / 2;
            return Convert.ToInt32(Math.Round(value / dbf) * dbf);
        }

        private bool _mousedown, _dragLeftEdgeMode, _selByRectMode;

        private HitInfo _dragEdgeInfo;

        private Plot _current;

        private void PictBox_MouseDown(object sender, MouseEventArgs e)
        {
            _mousedown = true;
            var eLocation = e.Location;
            HostPanel.Focus(); 
            switch (e.Button)
            {
                case MouseButtons.Left:
                    {
                        if (_testing) break;
                        // запоминание точки первого нажатия для выбора прямоугольником
                        _selpoint.X = ToGrid(eLocation.X);
                        _selpoint.Y = ToGrid(eLocation.Y);                      
                        var plot = GetPlotByBody(eLocation);
                        _current = plot;
                        if (plot == null)
                        {
                            UnselectedPlots();
                            UnselectedRemarks();
                            _selByRectMode = true;
                            _startselpoint = new Point(ToGrid(eLocation.X), ToGrid(eLocation.Y));
                            _endselpoint = new Point(ToGrid(eLocation.X), ToGrid(eLocation.Y));
                        }
                        else
                        {
                            _selByRectMode = false;
                            if (!_current.Selected && !_controlPressed)
                            {
                                UnselectedPlots();
                                UnselectedRemarks();
                            }
                            BuildUnionSelRect(plot);
                            _selrectpoint.X = _selrect.X;
                            _selrectpoint.Y = _selrect.Y;
                            if (e.Clicks > 1) // двойной щелчок
                            {
                                var remark = plot as Remark;
                                if (remark != null)
                                    EditRemark(remark);
                                var gateinp = plot as GateInput;
                                if (gateinp != null)
                                {
                                    if (plot.Contains(eLocation) == PlotHits.Descriptor)
                                        EditGateInputText(gateinp);
                                }
                                var gateout = plot as GateOutput;
                                if (gateout != null)
                                {
                                    if (plot.Contains(eLocation) == PlotHits.Descriptor)
                                        EditGateOutputText(gateout);
                                }
                                return;
                            }
                        }
                        plot = GetPlotByAll(eLocation);
                        if (plot != null)
                        {
                            _selByRectMode = false;
                            // начало перетаскивания выхода модуля
                            var info = plot.CheckMouseHitAt(eLocation);
                            switch (info.Hits)
                            {
                                case PlotHits.OutputLink:
                                    DoDragDrop(new DragedOutputInfo {Module = plot, HitInfo = info},
                                                DragDropEffects.Move);
                                    break;
                                case PlotHits.LeftEdge:
                                    _dragLeftEdgeMode = true;
                                    _dragEdgeInfo = info;
                                    _current = plot;
                                    Cursor = Cursors.VSplit;
                                    break;
                            }
                        }
                    }
                    break;
                case MouseButtons.Right:
                    {
                        Cursor = Cursors.Default;
                        //if (_testing) break;
                        _selByRectMode = false;
                        var plot = GetPlotByAll(eLocation);
                        if (plot != null)
                            plot.ShowPopupAt(PictBox, eLocation);
                    }
                    break;
            }
        }

        private void UnselectedPlots()
        {
            var query = from plt in _plotlist where plt.Selected select plt;
            foreach (var item in query) item.Selected = false;
        }

        private void UnselectedRemarks()
        {
            var query = from plt in _textlist where plt.Selected select plt;
            foreach (var item in query) item.Selected = false;
        }

        private void EditGateInputText(GateInput gateinp)
        {
            var frm = new InputValueForm { LinesValue = gateinp.Lines.ToArray() };
            if (frm.ShowDialog() != DialogResult.OK) return;
            gateinp.Lines.Clear();
            gateinp.Lines.AddRange(frm.LinesValue);
            PictBox.Invalidate();
            Changed(true);
        }

        private void EditGateOutputText(GateOutput gateout)
        {
            var frm = new InputValueForm { LinesValue = gateout.Lines.ToArray() };
            if (frm.ShowDialog() != DialogResult.OK) return;
            gateout.Lines.Clear();
            gateout.Lines.AddRange(frm.LinesValue);
            PictBox.Invalidate();
            Changed(true);
        }

        private void EditRemark(Remark remark)
        {
            var frm = new InputValueForm {LinesValue = remark.Lines.ToArray()};
            if (frm.ShowDialog() != DialogResult.OK) return;
            remark.Lines.Clear();
            remark.Lines.AddRange(frm.LinesValue);
            if (remark.Lines.Count == 0)
            {
                _textlist.Remove(remark);
                ReorderRemarks();
            }
            PictBox.Invalidate();
            Changed(true);
        }

        private void PictBox_MouseMove(object sender, MouseEventArgs e)
        {
            var eLocation = e.Location;
            if (e.Button == MouseButtons.Left && _mousedown && !_testing)
            {
                Cursor = _dragLeftEdgeMode ? Cursor = Cursors.VSplit : Cursors.Default;
                _selrect.Location = _selrectpoint;
                _selrect.Offset(ToGrid(eLocation.X) - _selpoint.X, ToGrid(eLocation.Y) - _selpoint.Y);
                if (_selByRectMode)
                    _endselpoint = new Point(ToGrid(eLocation.X), ToGrid(eLocation.Y));
                PictBox.Invalidate();
            }
            else
            {
                var plot = GetPlotByAll(eLocation);
                if (plot != null)
                {
                    var found = plot.Contains(eLocation);
                    switch (found)
                    {
                        case PlotHits.LeftEdge:
                            Cursor = _testing ? Cursors.Default : Cursors.VSplit;
                            break;
                        case PlotHits.Body:
                        case PlotHits.Caption:
                        case PlotHits.OrderNum:
                        case PlotHits.Descriptor:
                            Cursor = plot is GateInput && found != PlotHits.Descriptor
                                ? ((GateInput)plot).ExternalLinked() ? Cursors.Default : Cursors.Hand
                                : _testing ? Cursors.Default : Cursors.SizeAll;
                            break;
                        case PlotHits.InputLink:
                            var info = plot.CheckMouseHitAt(eLocation);
                            Cursor = info.InOut > 0 && plot.Inputs[info.InOut].Link == null ? Cursors.Hand : Cursors.Default;
                            break;
                        case PlotHits.OutputLink:
                            Cursor = Cursors.Arrow;
                            break;
                        default:
                            Cursor = Cursors.Default;
                            break;
                    }
                }
                else
                    Cursor = Cursors.Default;
            }
        }

        private void PictBox_MouseUp(object sender, MouseEventArgs e)
        {
            var eLocation = e.Location;
            if (e.Button != MouseButtons.Left || !_mousedown) return;
            _mousedown = false;
            if (!_testing && _dragLeftEdgeMode)
            {
                _dragLeftEdgeMode = false;
                var ofsX = ToGrid(eLocation.X) - _selpoint.X;
                var ofsY = ToGrid(eLocation.Y) - _selpoint.Y;
                if (ofsX != 0 || ofsY != 0)
                {
                    switch (_dragEdgeInfo.Hits)
                    {
                        case PlotHits.LeftEdge:
                            var plot = _current;
                            var point = plot.Outputs[_dragEdgeInfo.InOut]
                                .Link.Targets[_dragEdgeInfo.LinkIndex].FirstPoint;
                            point.X += ofsX;
                            if (point.X < 0) point.X = 0;
                            plot.Outputs[_dragEdgeInfo.InOut]
                                .Link.Targets[_dragEdgeInfo.LinkIndex].FirstPoint = point;
                            Changed(true);
                            break;
                    }
                }
                Cursor = Cursors.Default;
                _selrect = new Rectangle();
                _selByRectMode = false;
                PictBox.Invalidate();
                _controlPressed = false;
                return;
            }
            // перетаскивание закончено или клик
            _selrect.Location = _selrectpoint;
            if (eLocation.X != _selpoint.X || eLocation.Y != _selpoint.Y)
            {
                var ofsX = ToGrid(eLocation.X) - _selpoint.X;
                var ofsY = ToGrid(eLocation.Y) - _selpoint.Y;
                if (!_testing && (ofsX != 0 || ofsY != 0))
                {
                    _selrect.Offset(ofsX, ofsY);
                    if (_current != null && !_current.Selected)
                    {
                        UnselectedPlots();
                        UnselectedRemarks();
                        _current.Selected = true;
                    }
                    var list = new List<Plot>();
                    list.AddRange(_plotlist.Where(plt => plt.Selected));
                    list.AddRange(_textlist.Where(plt => plt.Selected));
                    if (!_controlPressed) // перемещение элементов
                    {
                        foreach (var plt in list)
                        {
                            var offset = plt.Location;
                            var dx = ToGrid(eLocation.X) - _selpoint.X;
                            offset.X += dx;
                            offset.Y += ToGrid(eLocation.Y) - _selpoint.Y;
                            plt.Location = offset;
                        }
                        // только для одиночного элемента
                        if (_plotlist.Count(plt => plt.Selected) == 1)
                            foreach (var plt in list)
                            {
                                // коррекция перегиба линии при перемещении по горизонтали
                                for (uint i = 1; i <= plt.Inputs.Length; i++)
                                {
                                    var input = plt.Inputs[i];
                                    if (input.Link == null) continue;
                                    var module = input.Link.Module.Outputs[1]
                                        .Link.Targets.FirstOrDefault(tar => tar.Module == plt);
                                    if (module == null) continue;
                                    if (module.FirstPoint.IsEmpty) continue;
                                    var point = module.FirstPoint;
                                    point.X += ofsX;
                                    if (point.X < 0) point.X = 0;
                                    module.FirstPoint = point;
                                }
                            }
                    }
                    else // копирование элементов
                    {
                        UnselectedPlots();
                        UnselectedRemarks();
                        var forlink = new List<Plot>();
                        foreach (var plt in list)
                        {
                            var offset = plt.Location;
                            offset.X += ToGrid(eLocation.X) - _selpoint.X;
                            offset.Y += ToGrid(eLocation.Y) - _selpoint.Y;
                            var coll = new NameValueCollection();
                            plt.SaveProperties(coll);
                            var newplot = (Plot) plt.Clone();
                            newplot.LoadProperties(coll);
                            newplot.OlderOrderNum = plt.OrderNum;
                            newplot.PageNum = PageNum;
                            newplot.Location = offset;
                            newplot.Selected = true;
                            if (plt is Remark)
                            {
                                _textlist.Add(newplot);
                                newplot.OrderNum = (uint) _textlist.Count;
                            }
                            else
                            {
                                _plotlist.Add(newplot);
                                newplot.OrderNum = (uint) _plotlist.Count;
                                ConnectLogicEvents(newplot);
                                forlink.Add(newplot);
                            }
                            BuildUnionSelRect(newplot);
                        }
                        ConnectLinks(forlink);
                    }
                    Changed(true);
                }
                else // координаты мыши не изменились, обработка "клика мышки"
                {
                    var plot = GetPlotByBody(eLocation);
                    if (plot == null)
                    {
                        UnselectedPlots();
                        UnselectedRemarks();
                    }
                    else
                    {
                        // выбор мышью существующего элемента
                        if (!_controlPressed && !plot.Selected)
                        {
                            UnselectedPlots();
                            UnselectedRemarks();
                        }
                        if (!plot.Selected)
                            plot.Selected = true;
                        else if (_controlPressed && plot.Selected)
                            plot.Selected = false;
                        BuildUnionSelRect(plot);
                        PictBox.Refresh();
                        _selrectpoint.X = _selrect.X;
                        _selrectpoint.Y = _selrect.Y;
                    }
                    plot = GetPlotByAll(eLocation);
                    if (plot != null)
                    {
                        if (plot.Contains(eLocation) == PlotHits.InputLink)
                        {
                            var info = plot.CheckMouseHitAt(eLocation);
                            if (!(info.Hits == PlotHits.InputLink && info.InOut > 0 &&
                                  plot.Inputs[info.InOut].Link != null))
                            {
                                // обработка клика мышки на входе
                                if (plot.ClickAt(eLocation) && !_testing) Changed(true);
                            }
                        }
                        else if (plot is GateInput)
                        {
                            var info = plot.CheckMouseHitAt(eLocation);
                            if (info.Hits == PlotHits.Body ||
                                info.Hits == PlotHits.Caption ||
                                info.Hits == PlotHits.OrderNum)
                            {
                                // обработка клика мышки на теле (кроме дескриптора)
                                if (plot.ClickAt(eLocation) && !_testing) Changed(true);
                                _selrect = new Rectangle();
                                _selByRectMode = false;
                                PictBox.Invalidate();
                                _controlPressed = false;
                                return;
                            }
                        }
                    }
                }
                if (!_testing && _selByRectMode) // выбор прямоугольником
                {
                    UnselectedPlots();
                    UnselectedRemarks();
                    var list = new List<Plot>();
                    var rect = CalcSelByRect();
                    list.AddRange(_plotlist.Where(plt => rect.Contains(Rectangle.Ceiling(plt.BoundsRect))));
                    list.AddRange(_textlist.Where(plt => rect.Contains(Rectangle.Ceiling(plt.BoundsRect))));
                    foreach (var plot in list)
                    {
                        plot.Selected = true;
                    }
                    _selByRectMode = false;
                }
            }
            _selrect = new Rectangle();
            PictBox.Invalidate();
            _controlPressed = false;
        }

        private bool _controlPressed;
        private static int _msecond;
        private bool _reloading = true;

        private void ChildForm_KeyDown(object sender, KeyEventArgs e)
        {
            _controlPressed = e.Control;
            if (_testing) return;
            if (e.KeyCode == Keys.V && e.Control || e.KeyCode == Keys.Insert && e.Shift)
                PasteFromClipboardAndSelected();
            else if (e.KeyCode == Keys.C && e.Control)
                CopySelectedToClipboard();
            else if (e.KeyCode == Keys.X && e.Control)
                CutSelectedToClipboard();
            else if (e.KeyCode == Keys.A && e.Control)
            {
                foreach (var plot in _plotlist)
                    plot.Selected = true;
                foreach (var plot in _textlist)
                    plot.Selected = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                if ((_plotlist.Any(plt => plt.Selected) ||
                     _textlist.Any(plt => plt.Selected)) &&
                    MessageBox.Show(this, @"Удалить выделенные элементы?",
                                    @"Удаление элементов", MessageBoxButtons.YesNoCancel,
                                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    foreach (var plot in _plotlist.Where(plt => plt.Selected))
                    {
                        RemoveSourceLinks(plot);
                        RemoveTargetLinks(plot);
                    }

                    foreach (var output in _plotlist.Where(plt => plt.Selected).OfType<GateOutput>())
                        output.RemoveGateOutputs();
                    foreach (var input in _plotlist.Where(plt => plt.Selected).OfType<GateInput>())
                        input.RemoveGateOutput();

                    _plotlist.RemoveAll(plt => plt.Selected);
                    ReorderPlots();
                    _textlist.RemoveAll(plt => plt.Selected);
                    ReorderRemarks();
                    PictBox.Refresh();
                    Changed(true);
                }
            }
        }

        private void ChildForm_KeyUp(object sender, KeyEventArgs e)
        {
            _controlPressed = e.Control;
        }

        private void PictBox_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = _testing ? DragDropEffects.None : DragDropEffects.Move;
        }

        private void PictBox_DragDrop(object sender, DragEventArgs e)
        {
            if (_testing) return;
            var eLocation = new Point(e.X, e.Y);
            HostPanel.Focus();
            // формирование связи между модулями (от выхода источника ко входу приёмника)
            if (e.Data.GetDataPresent("CheckLogic.DragedOutputInfo", false))
            {
                #region связывание выходов со входами
                var movedOutput = (DragedOutputInfo)e.Data.GetData("CheckLogic.DragedOutputInfo");
                var pt = ((PictureBox)sender).PointToClient(new Point(eLocation.X, eLocation.Y));
                var plot = GetPlotByAll(pt);
                if (plot != null /*&& movedOutput.Module != plot*/)
                {
                    var info = plot.CheckMouseHitAt(pt);
                    if (info.Hits == PlotHits.InputLink)
                    {
                        if (plot.Inputs[info.InOut].Link != null) return; // вход уже занят
                        if (plot.Inputs[info.InOut].Value != null &&
                            movedOutput.Module.Outputs[movedOutput.HitInfo.InOut].Value != null)
                        {
                            var typeinp = plot.Inputs[info.InOut].Value.GetType();
                            var typeout = movedOutput.Module.Outputs[movedOutput.HitInfo.InOut].Value.GetType();
                            if (typeinp != typeout)
                            {
                                MessageBox.Show(this, @"Нельзя связывать данные разных типов!",
                                    @"Ошибка создания связи", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                        }
                        string question;
                        MessageBoxIcon icon;
                        if (movedOutput.Module.OrderNum < plot.OrderNum)
                        {
                            question = "Связать выход {0} модуля {1} со входом {2} модуля {3}?";
                            icon = MessageBoxIcon.Question;
                        }
                        else
                        {
                            question =
                                "Модуль {1} выполняется позднее модуля {3}!\r\n" +
                                "Связать выход {0} модуля {1} со входом {2} модуля {3}?";
                            icon = MessageBoxIcon.Warning;
                        }
                        if (MessageBox.Show(this,
                                            String.Format(question,
                                                          movedOutput.HitInfo.InOut, 
                                                          movedOutput.Module.ModuleName(),
                                                          info.InOut, 
                                                          plot.ModuleName()
                                                          ),
                                            @"Создание связи", MessageBoxButtons.YesNoCancel,
                                            icon, MessageBoxDefaultButton.Button2) ==
                            DialogResult.Yes)
                        {
                            movedOutput.Module.AddTargetLink(movedOutput.HitInfo.InOut, 
                                new ModulePin { Module = plot, Pin = info.InOut, PageOrder = PageNum});
                            Changed(true);
                        }

                    }
                }
                #endregion связывание выходов со входами
            }
            else if (e.Data.GetDataPresent("System.Windows.Forms.TreeNode", false))
            {
                var movedNode = (TreeNode) e.Data.GetData("System.Windows.Forms.TreeNode");
                if (movedNode.Tag == null) return;
                var pt = ((PictureBox)sender).PointToClient(new Point(eLocation.X, eLocation.Y));
                var template = movedNode.Tag as Logic;
                if (template != null)
                {
                    var plot = (Logic) template.Clone();
                    plot.SetPlots(_plotlist);
                    plot.PageNum = PageNum;
                    ConnectLogicEvents(plot);
                    plot.Location = new PointF(ToGrid(pt.X), ToGrid(pt.Y));
                    _plotlist.Add(plot);
                    plot.OrderNum = (uint) _plotlist.Count;
                    var coll = new NameValueCollection();
                    plot.SaveProperties(coll);
                    plot.LoadProperties(coll);
                    ReorderPlots();
                    Changed(true);
                }
                var textblock = movedNode.Tag as Remark;
                if (textblock != null)
                {
                    var plot = (Remark)textblock.Clone();
                    plot.SetPlots(_textlist);
                    plot.PageNum = PageNum;
                    plot.Location = new PointF(ToGrid(pt.X), ToGrid(pt.Y));
                    _textlist.Add(plot);
                    plot.OrderNum = (uint)_textlist.Count;
                    var coll = new NameValueCollection();
                    plot.SaveProperties(coll);
                    plot.LoadProperties(coll);
                    ReorderRemarks();
                    PictBox.Refresh();
                    Changed(true);
                }
            }
            PictBox.Invalidate();
        }

        private void ConnectLogicEvents(Plot plot)
        {
            plot.OnInvalidate += (o, args) =>
                {
                    PictBox.Invalidate();
                    if (((ChangeEventArgs)args).Changed)
                        Changed(true);
                };
            plot.OnRemove += (o, args) =>
            {
                if (MessageBox.Show(this, @"Удалить элемент?",
                                    @"Удаление элементов", MessageBoxButtons.YesNoCancel,
                                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;
                _plotlist.Remove((Plot) o);
                RemoveSourceLinks((Plot) o);
                RemoveTargetLinks((Plot) o);
                var output = o as GateOutput;
                if (output != null)
                    output.RemoveGateOutputs();
                var input = o as GateInput;
                if (input != null)
                    input.RemoveGateOutput();
                ReorderPlots();
                Changed(true);
                PictBox.Invalidate();
            };
        }

        private static void RemoveTargetLinks(Plot module)
        {
            module.RemoveTargetLinkFor(module);
        }

        private static void RemoveSourceLinks(Plot module)
        {
            for (var n = 1; n <= module.Inputs.Length; n++)
                module.RemoveSourceLink((uint)n);
        }

        private void ReorderPlots()
        {
            var n = 1;
            foreach (var plot in _plotlist)
                plot.OrderNum = (uint) n++;
        }

        private void ReorderRemarks()
        {
            var n = 1;
            foreach (var plot in _textlist)
                plot.OrderNum = (uint)n++;
        }

        private void ChildForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            var exiter = MdiParent as IExit;
            if (exiter != null)
                exiter.OnChildHide(this);
            e.Cancel = exiter == null || !exiter.ExitEnabled();
            Hide();
        }
    }
}
