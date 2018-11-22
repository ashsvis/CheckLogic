using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace CheckLogic
{
    public partial class InputLinkForm : Form
    {
        public GateOutput Selected;

        private readonly GateOutput[] _list; 

        public InputLinkForm(List<GateOutput> list, GateOutput current)
        {
            InitializeComponent();
            listBox1.Items.Clear();
            _list = list.ToArray();
            foreach (var index in from plot in _list 
                                  let index = listBox1.Items.Add(
                                      String.Format("Лист {0}, L{1}.1 {2}{3}",
                                                    plot.PageNum,
                                                    plot.OrderNum,
                                                    (plot.Name != null ? "{"+plot.Name+"} " : ""), 
                                                    String.Join(" ", plot.Lines))) 
                                  where plot == current select index)
            {
                listBox1.SelectedIndex = index;
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex < 0)
            {
                btnOk.Enabled = false;
                return;
            }
            Selected = _list[listBox1.SelectedIndex];
            btnOk.Enabled = true;
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            if (Selected != null)
                DialogResult = DialogResult.OK;
        }
    }
}
