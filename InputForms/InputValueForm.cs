using System.Windows.Forms;
using System.Globalization;

namespace CheckLogic
{
    public partial class InputValueForm : Form
    {
        public InputValueForm()
        {
            InitializeComponent();
        }

        public string StrValue
        {
            get { return tbValue.Text; }
            set
            {
                Text = @"Введите строку символов";
                tbValue.Text = value;
                tbValue.Multiline = false;
                tbValue.TextAlign = HorizontalAlignment.Left;
                tbValue.SelectAll();
                AcceptButton = btnOk;
            }
        }

        public string[] LinesValue
        {
            get { return tbValue.Lines; }
            set
            {
                Text = @"Введите многострочный текст";
                tbValue.Lines = value;
                tbValue.Multiline = true;
                tbValue.TextAlign = HorizontalAlignment.Left;
                AcceptButton = null;
                Height += value.Length * 22;
                tbValue.SelectAll();
            }
        }

        private float _fvalue;

        public float FloatValue
        {
            get
            {
                Text = @"Введите натуральное число";
                var fp = CultureInfo.GetCultureInfo("en-US");
                float.TryParse(tbValue.Text.Replace(',', '.'), NumberStyles.Float, fp, out _fvalue);
                return _fvalue;
            }
            set
            {
                _fvalue = value;
                var fp = CultureInfo.GetCultureInfo("en-US");
                tbValue.Text = _fvalue.ToString(fp);
                tbValue.Multiline = false;
                tbValue.TextAlign = HorizontalAlignment.Right;
                AcceptButton = btnOk;
                tbValue.SelectAll();
            }
        }

        private int _ivalue;

        public int IntValue
        {
            get
            {
                Text = @"Введите целое число";
                var fp = CultureInfo.GetCultureInfo("en-US");
                int.TryParse(tbValue.Text, NumberStyles.Float, fp, out _ivalue);
                return _ivalue;
            }
            set
            {
                _ivalue = value;
                var fp = CultureInfo.GetCultureInfo("en-US");
                tbValue.Text = _ivalue.ToString(fp);
                tbValue.Multiline = false;
                tbValue.TextAlign = HorizontalAlignment.Right;
                AcceptButton = btnOk;
                tbValue.SelectAll();
            }
        }
    }
}
