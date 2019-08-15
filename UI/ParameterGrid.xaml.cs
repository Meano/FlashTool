using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SMTool.UI
{
    /// <summary>
    /// ParameterGrid 的交互逻辑
    /// </summary>
    public partial class ParameterGrid : Grid
    {
        #region Public
        public string KeyName;
        public Dictionary<string, string[]> Dependence;
        public Type ParaType;
        public delegate void ParaUpdateDelegate(string Key, string Value);
        public ParaUpdateDelegate ParaUpdate;

        public delegate string ParaGeneratorDelegate(ParameterWindow pw);
        public ParaGeneratorDelegate ParaGenerator;

        #region Window
        public ParameterWindow Owner;

        public void RegisterOwner(ParameterWindow pw)
        {
            Owner = pw;
        }

        public void Show()
        {
            Visibility = Visibility.Visible;
        }

        public void Hide()
        {
            Visibility = Visibility.Collapsed;
        }
        #endregion

        #region Value

        public string ParaValue
        {
            get
            {
                switch (ParaType.Name)
                {
                    case "TextBox":
                        return ParaValueString;
                    case "ComboBox":
                        return (ParaValueComboBox.SelectedIndex >= 0) ? ParaValueList[(string)ParaValueComboBox.SelectedValue] : null;
                    default:
                        return null;
                }
            }
            set
            {
                switch (ParaType.Name)
                {
                    case "TextBox":
                        ParaValueTextBox.Text = value;
                        break;
                    case "ComboBox":
                        ParaValueComboBox.Text = value;
                        break;
                    default:
                        break;
                }
            }
        }

        public bool IsComplete
        {
            get
            {
                if (Visibility != Visibility.Visible) {
                    return true;
                }
                switch (ParaType.Name)
                {
                    case "TextBox":
                        return ParaRegex.IsMatch(ParaValueString) && ParaValueString.Length == ParaLength || ParaLength == 0;
                    case "ComboBox":
                        return ParaValue != null;
                    default:
                        return false;
                }
            }
        }

        #endregion

        public Dictionary<string, string[]> GetDependence(string DependenceString)
        {
            Dictionary<string, string[]> DepList = new Dictionary<string, string[]>();
            if (DependenceString == null) return DepList;
            foreach (string SplitedString in DependenceString.Split(','))
            {
                int DotIndex = SplitedString.IndexOf('.');
                if (DotIndex <= 0) continue;
                string DepKey = SplitedString.Substring(0, DotIndex);
                string[] DepValue = SplitedString.Substring(DotIndex + 1).Split('|');
                DepList.Add(DepKey, DepValue);
            }
            return DepList;
        }
        #endregion

        #region TexBox
        private string ParaValueString_ = "";
        private string ParaValueString {
            get
            {
                return ParaValueString_;
            }
            set
            {
                if (ParaLength == 0 || (value.Length <= ParaLength && ParaRegex.IsMatch(value)))
                {
                    ParaValueString_ = value;
                }
            }
        }

        private string ParaInfo;
        private int ParaLength;
        private Regex ParaRegex;

        public bool IsParaReadOnly
        {
            set
            {
                ParaValueTextBox.IsReadOnly = value;
                ParaGeneratorButton.Visibility = ParaGenerator != null ? (value ? Visibility.Visible : Visibility.Hidden) : Visibility.Hidden;
                ParaGeneratorButton.Content = ParaGenerator != null ? ParaNameLabelText.Content : ParaGeneratorButton.Content;
            }
            get
            {
                return ParaValueTextBox.IsReadOnly;
            }
        }

        public bool IsInputEnabled{
            set
            {
                InputMethod.SetIsInputMethodEnabled(ParaValueTextBox, value);
            }
            get
            {
                return InputMethod.GetIsInputMethodEnabled(ParaValueTextBox);
            }
        }

        public ParameterGrid(string Key, string NameLabel, string RegexPattern, int Length, string Info, object Generator, string DependenceString)
        {
            InitializeComponent();
            KeyName = Key;

            Dependence = GetDependence(DependenceString);
            ParaType = ParaValueTextBox.GetType();
            ParaValueTextBox.Visibility = Visibility.Visible;
            ParaValueComboBox.Visibility = Visibility.Hidden;
            ParaInfoLabel.Visibility = Visibility.Visible;
 
            ParaNameLabelText.Content = NameLabel;
            ParaInfo = Info;
            ParaInfoLabelText.Text = Info;
            ParaRegex = new Regex(RegexPattern);
            ParaLength = Length;
            if(Generator != null)
            {
                if(Generator is string)
                    ParaValueTextBox.Text = (string)Generator;
                else if(Generator is ParaGeneratorDelegate)
                {
                    ParaGenerator = (ParaGeneratorDelegate)Generator;
                }
            }
        }

        public ParameterGrid(string Key, string NameLabel, string RegexPattern, int Length, string Info, object Default, string DependenceString, bool IsReadOnly) :
            this(Key, NameLabel, RegexPattern, Length, Info, Default, DependenceString)
        {
            IsParaReadOnly = IsReadOnly;
        }

        public ParameterGrid(string Key, string NameLabel, string RegexPattern, int Length, string Info, object Default, string DependenceString, bool IsReadOnly, string ParaButtonName) :
            this(Key, NameLabel, RegexPattern, Length, Info, Default, DependenceString, IsReadOnly)
        {
            ParaGeneratorButton.Content = ParaButtonName;
        }
        private void ParaNameLabel_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (ParaGenerator != null)
            {
                ParaGeneratorButton.Visibility = Visibility.Visible;
            }
        }

        private void ParaNameLabel_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!IsParaReadOnly)
            {
                ParaGeneratorButton.Visibility = Visibility.Hidden;
            }
        }

        private void ParaGeneratorButton_Click(object sender, RoutedEventArgs e)
        {
            string GeneratedString = ParaGenerator?.Invoke(Owner);
            if (GeneratedString != null)
            {
                ParaValueTextBox.Text = GeneratedString;
            }
            else
            {
                MessageBox.Show("生成失败！");
            }
        }

        private void ParaValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string ChangedText = ParaValueTextBox.Text;
            ParaInfoLabelText.Text = ParaInfo;
            if (ParaRegex.IsMatch(ChangedText))
            {
                ParaInfoLabelText.Foreground = Brushes.Black;
                if(ParaLength == 0)
                {
                    if(ChangedText.Length > 0)
                    {
                        ParaInfoLabel.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        ParaInfoLabel.Visibility = Visibility.Visible;
                    }
                    ParaValueString = ChangedText;
                }
                else if (ChangedText.Length == ParaLength)
                {
                    ParaValueString = ChangedText;
                    ParaInfoLabel.Visibility = Visibility.Collapsed;
                }
                else if (ChangedText.Length > ParaLength)
                {
                    ParaValueTextBox.Text = ParaValueString;
                    ParaValueTextBox.SelectionStart = ParaValueString.Length;
                }
                else
                {
                    ParaValueString = ChangedText;
                    ParaInfoLabelText.Text += "当前字符串长度" + ChangedText.Length.ToString() + "不足" + ParaLength.ToString();
                    ParaInfoLabel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                ParaValueTextBox.Text = ParaValueString;
                ParaValueTextBox.SelectionStart = ParaValueString.Length;

                ParaInfoLabelText.Foreground = Brushes.Red;
                ParaInfoLabelText.Text = "请勿输入不允许的字符！";
                ParaInfoLabel.Visibility = Visibility.Visible;
            }
            ParaUpdate?.Invoke(KeyName, ParaValue);
        }

        private void ParaValueTextBox_PreviewDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Move;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void ParaValueTextBox_PreviewDrop(object sender, DragEventArgs e)
        {
            string FileName = ((Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
            MessageBox.Show(FileName);
        }
        #endregion

        #region ComboBox
        private Dictionary<string, string> ParaValueList;
        public ParameterGrid(string Key, string NameLabel, string ValueInfo, Dictionary<string,string> ValueList, string Default, string DependenceString)
        {
            InitializeComponent();
            KeyName = Key;
            Dependence = GetDependence(DependenceString);
            ParaType = ParaValueComboBox.GetType();
            ParaValueTextBox.Visibility = Visibility.Hidden;
            ParaValueComboBox.Visibility = Visibility.Visible;
            ParaInfoLabel.Visibility = Visibility.Visible;

            ParaValueList = ValueList;
            foreach (KeyValuePair<string, string> keypair in ParaValueList)
            {
                ParaValueComboBox.Items.Add(keypair.Key);
            }
            ParaNameLabelText.Content = NameLabel;
            ParaInfoLabelText.Text = ValueInfo;
            if(Default != null)
            {
                ParaValueComboBox.Text = Default;
            }
        }

        private void ParaValueComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ParaValueComboBox.SelectedIndex >= 0)
            {
                ParaInfoLabel.Visibility = Visibility.Collapsed;
            }
            else
            {
                ParaInfoLabel.Visibility = Visibility.Visible;
            }
            ParaUpdate?.Invoke(KeyName, ParaValue);
        }
        #endregion
    }
}
