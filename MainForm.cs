using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CheckLogic
{
    public interface IExit
    {
        bool ExitEnabled();
        void OnChildHide(Form child);
    }

    public interface IChanged
    {
        void Changed(bool value);
    }

    public interface IClipboardSupport
    {
        void Copy(IEnumerable<Plot> items);
        void Cut(IEnumerable<Plot> items);
        IEnumerable<Plot> Paste();
        int PastesCount();
    }

    public interface IStopFetch
    {
        void Done();
    }

    public partial class MainForm : Form, IExit, IChanged, IClipboardSupport
    {
        public MainForm()
        {
            InitializeComponent();
            LoadLibrary();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private List<Plot> GetAllPlots()
        {
            var list = new List<Plot>();
            foreach (var child in MdiChildren.OfType<ChildForm>()
                .Where(child => !child.Emulation))
            {
                list.AddRange(child.PlotList);
            }
            return list;
        }

        private void tsbCreate_Click(object sender, EventArgs e)
        {
            var n = MdiChildren.OfType<ChildForm>().Count(item => !item.Emulation) + 1;
            var pagename = String.Format("Лист {0}", n);
            var cf = new ChildForm(GetAllPlots, false, _plugins)
                {
                    MdiParent = this,
                    PageNum = (uint) n,
                    PageName = "P" + n,
                    Text = pagename + @" (проект)",
                };
            cf.Show();
            var node = tvProject.Nodes.Find("Project", false).FirstOrDefault();
            if (node == null)
            {
                node = new TreeNode("Проект") { Name = "Project" };
                tvProject.Nodes.Add(node);                
            }
            var child = new TreeNode { Name = "P" + n, Text = @"Лист " + n, Tag = cf, ImageIndex = 2, SelectedImageIndex = 2 };
            node.Nodes.Add(child);
            child.EnsureVisible();
            tvProject.SelectedNode = child;
            //---------------------------------
            cf = new ChildForm(GetAllPlots, true, _plugins)
                {
                    MdiParent = this,
                    PageNum = (uint) n,
                    PageName = "P" + n,
                    Text = pagename + @" (эмуляция)",
                };
            node = tvProject.Nodes.Find("Emulation", false).FirstOrDefault();
            if (node == null)
            {
                node = new TreeNode("Эмуляция") { Name = "Emulation" };
                tvProject.Nodes.Add(node);                
            }
            child = new TreeNode { Name = "P" + n, Text = @"Лист " + n, Tag = cf, ImageIndex = 1, SelectedImageIndex = 1 };
            node.Nodes.Add(child);
            child.EnsureVisible();
        }

        private string _filename;

        private void tsbSave_Click(object sender, EventArgs e)
        {
            if (_filename != null)
            {
                SaveProject(_filename);
                Changed(false);
            }
            else
                tsbSaveAs_Click(null, null);
        }

        private void tsbSaveAs_Click(object sender, EventArgs e)
        {
            saveFileDialog1.FileName = Path.GetFileName(_filename ?? 
                Path.ChangeExtension(Application.ExecutablePath, ".lgk"));
            if (saveFileDialog1.ShowDialog() != DialogResult.OK) return;
            _filename = saveFileDialog1.FileName;
            SaveProject(_filename);
            Changed(false);
        }

        private void SaveProject(string filename)
        {
            var mif = new MemIniFile(filename);
            mif.Clear();
            foreach (var mdiChild in MdiChildren.Cast<ChildForm>()
                .Where(mdiChild => !mdiChild.Emulation))
                mdiChild.SaveContent(mif);
            mif.UpdateFile();
            UpdateEmulationForms(mif);
        }

        private void UpdateEmulationForms(MemIniFile mif)
        {
            foreach (var cf in MdiChildren.Cast<ChildForm>().Where(mdiChild => mdiChild.Emulation))
            {
                cf.LoadContent(mif);
            }
            LinkOutdoors(true);
        }

        private void tsbLoad_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;
            var filename = openFileDialog1.FileName;
            if (!File.Exists(filename)) return;
            Text = filename;
            _filename = filename;
            _exitEnabled = true;
            try
            {
                tvProject.Nodes.Clear();
                for (var i = MdiChildren.Length - 1; i >= 0; i--)
                {
                    var done = MdiChildren[i] as IStopFetch;
                    if (done != null) done.Done();
                    MdiChildren[i].Close();
                }
            }
            finally
            {
                _exitEnabled = false;
            }
            var mif = new MemIniFile(filename);
            var pages = new List<string>();
            foreach (var page in mif.ReadSections()
                                    .Select(section => section.Split(new[] { '.' })[0])
                                    .Where(page => !pages.Contains(page)))
            {
                pages.Add(page);
            }
            var testNodes = new TreeNode("Проект") { Name = "Project" };
            var n = 1;
            foreach (var cf in pages.Select(page => new ChildForm(GetAllPlots, false, _plugins) { MdiParent = this }))
            {
                var pagename = String.Format("Лист {0}", n);
                var node = new TreeNode {Name = "P" + n, Text = pagename, Tag = cf, ImageIndex = 1, SelectedImageIndex = 1};
                testNodes.Nodes.Add(node);
                cf.PageNum = (uint)n;
                cf.PageName = "P" + n;
                cf.Text = pagename + @" (проект)";
                cf.LoadContent(mif);
                if (n == 1)
                {
                    node.ImageIndex = 2;
                    node.SelectedImageIndex = 2;
                    cf.Show();
                    cf.WindowState = FormWindowState.Maximized;
                }
                n++;
            }
            testNodes.Expand();
            tvProject.Nodes.Add(testNodes);
            var emuNodes = new TreeNode("Эмуляция") { Name = "Emulation" };
            n = 1;
            foreach (var cf in pages.Select(page => new ChildForm(GetAllPlots, true, _plugins) { MdiParent = this }))
            {
                var pagename = String.Format("Лист {0}", n);
                emuNodes.Nodes.Add(new TreeNode { Name = "P" + n, Text = pagename, Tag = cf, ImageIndex = 1, SelectedImageIndex = 1 });
                cf.PageNum = (uint)n;
                cf.PageName = "P" + n;
                cf.Text = pagename + @" (эмуляция)";
                cf.LoadContent(mif);
                n++;
            }
            emuNodes.Expand();
            tvProject.Nodes.Add(emuNodes);
            var kinds = new[] {false, true};
            foreach (var emulation in kinds)
            {
                // подключение внешних связей
                var emul= emulation;
                LinkOutdoors(emul);
            }
            Changed(false);
        }

        private void LinkOutdoors(bool emul)
        {
            foreach (var amodule in from child in MdiChildren.OfType<ChildForm>()
                                    where child.Emulation == emul
                                    from link in child.ExternalOutputLinks
                                    select link.Split(new[] {','})
                                    into alink
                                    from module in alink
                                    select module.Split(new[] {'.'})
                                    into amodule
                                    where amodule.Length == 6
                                    select amodule)
            {
                uint page, ordernum, output, targetpage, targetordernum, targetpin;
                if (!uint.TryParse(amodule[0], out page) || !uint.TryParse(amodule[1], out ordernum) ||
                    !uint.TryParse(amodule[2], out output) || !uint.TryParse(amodule[3], out targetpage) ||
                    !uint.TryParse(amodule[4], out targetordernum) || !uint.TryParse(amodule[5], out targetpin))
                    continue;
                if (page > MdiChildren.Length) continue;
                var sourceform = MdiChildren.Cast<ChildForm>()
                                            .FirstOrDefault(frm => frm.PageNum == page && frm.Emulation == emul);
                var targetform = MdiChildren.Cast<ChildForm>()
                                            .FirstOrDefault(frm => frm.PageNum == targetpage && frm.Emulation == emul);
                if (sourceform == null || targetform == null) continue;
                var plot = sourceform.GetModuleByOrderNum(page, ordernum);
                var target = targetform.GetModuleByOrderNum(targetpage, targetordernum);
                if (plot != null && target != null)
                    plot.AddTargetLink(output,
                                       new ModulePin {Module = target, Pin = targetpin});
            }
            // подключение внешних связей от выходов ко входам
            foreach (var amodule in MdiChildren.OfType<ChildForm>().Where(chld => chld.Emulation == emul)
                                               .SelectMany(child1 => child1.ExternalGateLinks
                                                                           .Select(
                                                                               module => module.Split(new[] {'.'}))
                                                                           .Where(amodule => amodule.Length == 6)))
            {
                uint page, ordernum, output, targetpage, targetordernum, targetpin;
                if (!uint.TryParse(amodule[0], out page) ||
                    !uint.TryParse(amodule[1], out ordernum) ||
                    !uint.TryParse(amodule[2], out output) ||
                    !uint.TryParse(amodule[3], out targetpage) ||
                    !uint.TryParse(amodule[4], out targetordernum) ||
                    !uint.TryParse(amodule[5], out targetpin))
                    continue;
                if (page > MdiChildren.Length) continue;
                var sourceform =
                    MdiChildren.Cast<ChildForm>().FirstOrDefault(frm => frm.PageNum == page && frm.Emulation == emul);
                var targetform =
                    MdiChildren.Cast<ChildForm>().FirstOrDefault(frm => frm.PageNum == targetpage && frm.Emulation == emul);
                if (sourceform == null || targetform == null) continue;
                var gateoutput = sourceform.GetModuleByOrderNum(page, ordernum) as GateOutput;
                var gateinput = targetform.GetModuleByOrderNum(targetpage, targetordernum) as GateInput;
                if (gateoutput != null && gateinput != null)
                    gateoutput.AddGateInput(gateinput);
            }
        }

        private void tsbExit_Click(object sender, EventArgs e)
        {
            Close();
        }

        public bool ExitEnabled()
        {
            return _exitEnabled;
        }

        public void OnChildHide(Form child)
        {
            foreach (var category in tvProject.Nodes.Cast<TreeNode>())
            {
                foreach (var node in category.Nodes.Cast<TreeNode>().Where(node => node.Tag == child))
                {
                    node.ImageIndex = 1;
                    node.SelectedImageIndex = 1;
                    break;
                }
            }
        }

        private bool _exitEnabled;

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _exitEnabled = true;
            for (var i = MdiChildren.Length - 1; i >= 0; i--)
            {
                var done = MdiChildren[i] as IStopFetch;
                if (done != null) done.Done();
                MdiChildren[i].Close();
            }
            Application.ExitThread();
        }

        private void tsmiMdiWindow_DropDownOpening(object sender, EventArgs e)
        {
            tsmiMdiWindow.DropDownItems.Clear();
            if (MdiChildren.Length == 0) return;
            var item = new ToolStripMenuItem("Горизонтально");
            item.Click += (o, args) => LayoutMdi(MdiLayout.TileHorizontal);
            tsmiMdiWindow.DropDownItems.Add(item);
            item = new ToolStripMenuItem("Вертикально");
            item.Click += (o, args) => LayoutMdi(MdiLayout.TileVertical);
            tsmiMdiWindow.DropDownItems.Add(item);
            item = new ToolStripMenuItem("Каскадом");
            item.Click += (o, args) => LayoutMdi(MdiLayout.Cascade);
            tsmiMdiWindow.DropDownItems.Add(item);
            tsmiMdiWindow.DropDownItems.Add(new ToolStripSeparator());
            var nproj = 1;
            foreach (var child in MdiChildren.OfType<ChildForm>().Where(form => !form.Emulation))
            {
                item = new ToolStripMenuItem(
                    String.Format("{0} (лист проекта {1})", child.PageName, nproj))
                    {
                        Tag = child,
                        Checked = child == ActiveMdiChild
                    };
                item.Click += (o, args) =>
                    {
                        var frm = (ChildForm) ((ToolStripMenuItem) o).Tag;
                        frm.Show();
                        frm.BringToFront();
                        frm.WindowState = FormWindowState.Maximized;
                        foreach (var category in tvProject.Nodes.Cast<TreeNode>())
                        {
                            foreach (var node in category.Nodes.Cast<TreeNode>().Where(node => node.Tag == frm))
                            {
                                node.ImageIndex = 2;
                                node.SelectedImageIndex = 2;
                                break;
                            }
                        }
                    };
                tsmiMdiWindow.DropDownItems.Add(item);
                nproj++;
            }
            tsmiMdiWindow.DropDownItems.Add(new ToolStripSeparator());
            var nemul = 1;
            foreach (var child in MdiChildren.OfType<ChildForm>().Where(form => form.Emulation))
            {
                item = new ToolStripMenuItem(
                    String.Format("{0} (лист эмуляции {1})", child.PageName, nemul))
                {
                    Tag = child,
                    Checked = child == ActiveMdiChild
                };
                item.Click += (o, args) =>
                {
                    var frm = (ChildForm)((ToolStripMenuItem)o).Tag;
                    frm.Show();
                    frm.BringToFront();
                    frm.WindowState = FormWindowState.Maximized;
                    foreach (var category in tvProject.Nodes.Cast<TreeNode>())
                    {
                        foreach (var node in category.Nodes.Cast<TreeNode>().Where(node => node.Tag == frm))
                        {
                            node.ImageIndex = 2;
                            node.SelectedImageIndex = 2;
                            break;
                        }
                    }
                };
                tsmiMdiWindow.DropDownItems.Add(item);
                nemul++;
            }
        }

        public void Changed(bool value)
        {
            tsbSave.Enabled = value;
            tsmiSave.Enabled = value;
        }

        private readonly List<Plot> _cutlist = new List<Plot>();

        public void Copy(IEnumerable<Plot> items)
        {
            _cutlist.Clear();
            var collection = items as Plot[] ?? items.ToArray();
            _cutlist.AddRange(collection);
            _pastescount = 0;
        }

        public void Cut(IEnumerable<Plot> items)
        {
            _cutlist.Clear();
            var collection = items as Plot[] ?? items.ToArray();
            _cutlist.AddRange(collection);
            _pastescount = 0;
        }

        public IEnumerable<Plot> Paste()
        {
            _pastescount++;
            var result = _cutlist;
            return result;
        }

        private int _pastescount;

        public int PastesCount()
        {
            return _pastescount;
        }

        private void tvProject_MouseDown(object sender, MouseEventArgs e)
        {
            tvProject.SelectedNode = tvProject.GetNodeAt(e.Location);
        }

        private void tvProject_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) return;
            var node = tvProject.SelectedNode;
            if (node == null) return;
            var frm = node.Tag as Form;
            if (frm == null) return;
            node.ImageIndex = 2;
            node.SelectedImageIndex = 2;
            frm.Show();
            frm.BringToFront();
            frm.WindowState = FormWindowState.Maximized;
        }

        private readonly IDictionary<string, Type> _plugins = new Dictionary<string, Type>(); 

        private void LoadLibrary()
        {
            tvLibrary.Nodes.Clear();
            var ndText = new TreeNode("Текстовые блоки");
            tvLibrary.Nodes.Add(ndText);
            ndText.Nodes.Add(new TreeNode("Комментарий") { Tag = new Remark(null) });
            _plugins.Add("Remark", typeof(Remark));
            var ndInputGate = new TreeNode("Входные сигналы");
            tvLibrary.Nodes.Add(ndInputGate);
            _plugins.Add("GateInput", typeof(GateInput));
            ndInputGate.Nodes.Add(new TreeNode("Переключатель") { Tag = new GateInput(null, GateInputKind.Fixed) });
            ndInputGate.Nodes.Add(new TreeNode("Импульс") { Tag = new GateInput(null, GateInputKind.Impulse) });
            ndInputGate.Nodes.Add(new TreeNode("Сброс неисправности") { Tag = new GateInput(null, GateInputKind.Reset) });
            ndInputGate.Nodes.Add(new TreeNode("Дискретный вход") { Tag = new GateInput(null, GateInputKind.DI) });
            ndInputGate.Nodes.Add(new TreeNode("Аналоговый вход") { Tag = new GateInput(null, GateInputKind.AI) });
            var ndLogic = new TreeNode("Логика");
            tvLibrary.Nodes.Add(ndLogic);
            _plugins.Add("Logic", typeof(Logic));
            ndLogic.Nodes.Add(new TreeNode("Инвертор") { Tag = new Logic(null, LogicKind.Not) });
            var ndOr = new TreeNode("Дизъюнкция (\"ИЛИ\")");
            ndLogic.Nodes.Add(ndOr);
            ndOr.Nodes.Add(new TreeNode("2x") { Tag = new Logic(null, LogicKind.Or2) });
            ndOr.Nodes.Add(new TreeNode("3x") { Tag = new Logic(null, LogicKind.Or3) });
            ndOr.Nodes.Add(new TreeNode("4x") { Tag = new Logic(null, LogicKind.Or4) });
            ndOr.Nodes.Add(new TreeNode("5x") { Tag = new Logic(null, LogicKind.Or5) });
            ndOr.Nodes.Add(new TreeNode("6x") { Tag = new Logic(null, LogicKind.Or6) });
            ndOr.Nodes.Add(new TreeNode("7x") { Tag = new Logic(null, LogicKind.Or7) });
            ndOr.Nodes.Add(new TreeNode("8x") { Tag = new Logic(null, LogicKind.Or8) });
            var ndAnd = new TreeNode("Конъюнкция (\"И\")");
            ndLogic.Nodes.Add(ndAnd);
            ndAnd.Nodes.Add(new TreeNode("2x") { Tag = new Logic(null, LogicKind.And2) });
            ndAnd.Nodes.Add(new TreeNode("3x") { Tag = new Logic(null, LogicKind.And3) });
            ndAnd.Nodes.Add(new TreeNode("4x") { Tag = new Logic(null, LogicKind.And4) });
            ndAnd.Nodes.Add(new TreeNode("5x") { Tag = new Logic(null, LogicKind.And5) });
            ndAnd.Nodes.Add(new TreeNode("6x") { Tag = new Logic(null, LogicKind.And6) });
            ndAnd.Nodes.Add(new TreeNode("7x") { Tag = new Logic(null, LogicKind.And7) });
            ndAnd.Nodes.Add(new TreeNode("8x") { Tag = new Logic(null, LogicKind.And8) });
            ndLogic.Nodes.Add(new TreeNode("Исключающее \"ИЛИ\"") { Tag = new Logic(null, LogicKind.Xor) });
            var ndTrigger = new TreeNode("Триггеры");
            ndLogic.Nodes.Add(ndTrigger);
            ndTrigger.Nodes.Add(new TreeNode("RS-триггер") { Tag = new Logic(null, LogicKind.RsTrigger) });
            ndTrigger.Nodes.Add(new TreeNode("SR-триггер") { Tag = new Logic(null, LogicKind.SrTrigger) });
            ndLogic.Nodes.Add(new TreeNode("Детектор фронта") { Tag = new Logic(null, LogicKind.FrontEdge) });
            var ndTimer = new TreeNode("Таймеры");
            ndLogic.Nodes.Add(ndTimer);
            _plugins.Add("Timer", typeof(Timer));
            ndTimer.Nodes.Add(new TreeNode("Задержка включения") { Tag = new Timer(null, TimerKind.DelayOn) { Seconds = 1 } });
            ndTimer.Nodes.Add(new TreeNode("Задержка выключения") { Tag = new Timer(null, TimerKind.DelayOff) { Seconds = 1 } });
            ndTimer.Nodes.Add(new TreeNode("Формитователь импульса") { Tag = new Timer(null, TimerKind.OnePulse) { Seconds = 1 } });
            ndTimer.Nodes.Add(new TreeNode("Измеритель времени импульса") { Tag = new Timer(null, TimerKind.Measure) { Seconds = 1 } });
            ndTimer.Nodes.Add(new TreeNode("Текущее время") { Tag = new Timer(null, TimerKind.Time) { Seconds = 1 } });
            ndTimer.Nodes.Add(new TreeNode("Текущая дата") { Tag = new Timer(null, TimerKind.Date) { Seconds = 1 } });
            var ndComparator = new TreeNode("Компараторы");
            ndLogic.Nodes.Add(ndComparator);
            _plugins.Add("Comparator", typeof(Comparator));
            ndComparator.Nodes.Add(new TreeNode("Вход равен уставке") { Tag = new Comparator(null, CompareKind.EQ) });
            ndComparator.Nodes.Add(new TreeNode("Вход не равен уставке") { Tag = new Comparator(null, CompareKind.NE) });
            ndComparator.Nodes.Add(new TreeNode("Вход меньше уставки") { Tag = new Comparator(null, CompareKind.LT) });
            ndComparator.Nodes.Add(new TreeNode("Вход меньше или равен уставке") { Tag = new Comparator(null, CompareKind.LE) });
            ndComparator.Nodes.Add(new TreeNode("Вход больше уставки") { Tag = new Comparator(null, CompareKind.GT) });
            ndComparator.Nodes.Add(new TreeNode("Вход больше или равен уставке") { Tag = new Comparator(null, CompareKind.GE) });
            var ndSelector = new TreeNode("Селекторы");
            ndLogic.Nodes.Add(ndSelector);
            _plugins.Add("Selector", typeof(Selector));
            ndSelector.Nodes.Add(new TreeNode("Выбор дискретного сигнала") { Tag = new Selector(null, SelectKind.Digital) });
            ndSelector.Nodes.Add(new TreeNode("Выбор аналогового сигнала") { Tag = new Selector(null, SelectKind.Analog) });
            var ndGenerator = new TreeNode("Генераторы");
            tvLibrary.Nodes.Add(ndGenerator);
            _plugins.Add("Generator", typeof(Generator));
            ndGenerator.Nodes.Add(new TreeNode("Дискретный меандр") { Tag = new Generator(null, GeneratorKind.DigitalMeandre) });
            ndGenerator.Nodes.Add(new TreeNode("Аналоговый меандр") { Tag = new Generator(null, GeneratorKind.AnalogMeandre) });
            ndGenerator.Nodes.Add(new TreeNode("Нарастающий") { Tag = new Generator(null, GeneratorKind.Growing) });
            ndGenerator.Nodes.Add(new TreeNode("Убывающий") { Tag = new Generator(null, GeneratorKind.Waning) });

            var ndMathCalc = new TreeNode("Математика");
            tvLibrary.Nodes.Add(ndMathCalc);
            _plugins.Add("MathCalc", typeof(MathCalc));
            ndMathCalc.Nodes.Add(new TreeNode("Ограничение значения") { Tag = new MathCalc(null, MathKind.LMT) });
            ndMathCalc.Nodes.Add(new TreeNode("Абсолютное значение") { Tag = new MathCalc(null, MathKind.ABS) });
            ndMathCalc.Nodes.Add(new TreeNode("Изменение знака") { Tag = new MathCalc(null, MathKind.NEG) });
            ndMathCalc.Nodes.Add(new TreeNode("Минимальное из двух") { Tag = new MathCalc(null, MathKind.MIN) });
            ndMathCalc.Nodes.Add(new TreeNode("Максимальное из двух") { Tag = new MathCalc(null, MathKind.MAX) });
            ndMathCalc.Nodes.Add(new TreeNode("Среднее из двух") { Tag = new MathCalc(null, MathKind.AVG) });
            ndMathCalc.Nodes.Add(new TreeNode("Среднее за 10 сек") { Tag = new MathCalc(null, MathKind.RLAVG) });
            var ndAriphmetic = new TreeNode("Арифметика");
            ndMathCalc.Nodes.Add(ndAriphmetic);
            ndAriphmetic.Nodes.Add(new TreeNode("Сложение") { Tag = new MathCalc(null, MathKind.ADD) });
            ndAriphmetic.Nodes.Add(new TreeNode("Вычитание") { Tag = new MathCalc(null, MathKind.SUB) });
            ndAriphmetic.Nodes.Add(new TreeNode("Умножение") { Tag = new MathCalc(null, MathKind.MUL) });
            ndAriphmetic.Nodes.Add(new TreeNode("Деление") { Tag = new MathCalc(null, MathKind.DIV) });
            ndAriphmetic.Nodes.Add(new TreeNode("Остаток от деления") { Tag = new MathCalc(null, MathKind.MOD) });
            ndMathCalc.Nodes.Add(new TreeNode("Округление к верхнему") { Tag = new MathCalc(null, MathKind.ROND) });
            ndMathCalc.Nodes.Add(new TreeNode("Округление к нижнему") { Tag = new MathCalc(null, MathKind.TRNC) });
            var ndPower = new TreeNode("Степенные");
            ndMathCalc.Nodes.Add(ndPower);
            ndPower.Nodes.Add(new TreeNode("Извлечение корня") { Tag = new MathCalc(null, MathKind.SQRT) });
            ndPower.Nodes.Add(new TreeNode("Степень числа") { Tag = new MathCalc(null, MathKind.POW) });
            ndPower.Nodes.Add(new TreeNode("Экспонента числа") { Tag = new MathCalc(null, MathKind.EXP) });
            var ndLogariphm = new TreeNode("Логарифмы");
            ndMathCalc.Nodes.Add(ndLogariphm);
            ndLogariphm.Nodes.Add(new TreeNode("Натуральный") { Tag = new MathCalc(null, MathKind.LN) });
            ndLogariphm.Nodes.Add(new TreeNode("Десятичный") { Tag = new MathCalc(null, MathKind.LOG) });
            var ndOutputGate = new TreeNode("Выходные сигналы");
            tvLibrary.Nodes.Add(ndOutputGate);
            _plugins.Add("GateOutput", typeof(GateOutput));
            ndOutputGate.Nodes.Add(new TreeNode("Лампа") { Tag = new GateOutput(null, GateOutputKind.Lamp) });
            ndOutputGate.Nodes.Add(new TreeNode("Звуковой сигнал") { Tag = new GateOutput(null, GateOutputKind.Sound) });
            ndOutputGate.Nodes.Add(new TreeNode("Дискретный выход") { Tag = new GateOutput(null, GateOutputKind.DO) });
            ndOutputGate.Nodes.Add(new TreeNode("Аналоговый выход") { Tag = new GateOutput(null, GateOutputKind.AO) });
            var ndEmulate = new TreeNode("Эмуляция");
            tvLibrary.Nodes.Add(ndEmulate);
            _plugins.Add("Emulate", typeof(Emulate));
            ndEmulate.Nodes.Add(new TreeNode("Задвижка") { Tag = new Emulate(null, EmulateKind.Latch) });
            //ndEmulate.Nodes.Add(new TreeNode("Клапан") { Tag = new Emulate(null, EmulateKind.Valve) });
            //ndEmulate.Nodes.Add(new TreeNode("Насос") { Tag = new Emulate(null, EmulateKind.Pump) });
            //-------------------
            tvLibrary.MouseDown += (sender, args) =>
                {
                    tvLibrary.SelectedNode = tvLibrary.GetNodeAt(args.Location);
                };
            tvLibrary.ItemDrag += (sender, args) =>
                {
                    var treeNode = args.Item as TreeNode;
                    if (treeNode != null && treeNode.Tag != null)
                        DoDragDrop(args.Item, DragDropEffects.Move);
                };
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            var child = ActiveMdiChild as ChildForm;
            if (child != null)
            {
                miPaste.Enabled = tsbPaste.Enabled = !child.Emulation && _cutlist.Count > 0;
                miCopy.Enabled = tsbCopy.Enabled = miCut.Enabled = tsbCut.Enabled = !child.Emulation && child.HasSelected();
            }
            else
            {
                miPaste.Enabled = tsbPaste.Enabled = false;
                miCopy.Enabled = tsbCopy.Enabled = miCut.Enabled = tsbCut.Enabled = false;
            }
        }

        private void tsbCut_Click(object sender, EventArgs e)
        {
            var child = ActiveMdiChild as ChildForm;
            if (child != null && !child.Emulation)
                child.CutSelectedToClipboard();
        }

        private void tsbCopy_Click(object sender, EventArgs e)
        {
            var child = ActiveMdiChild as ChildForm;
            if (child != null && !child.Emulation)
                child.CopySelectedToClipboard();
        }

        private void tsbPaste_Click(object sender, EventArgs e)
        {
            var child = ActiveMdiChild as ChildForm;
            if (child != null && !child.Emulation)
                child.PasteFromClipboardAndSelected();
        }
    }
}
