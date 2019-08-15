using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SMTool.UI
{
    public class ParaValueSetting
    {
        public string KeyName;
        public string Regex;
        public int Length;
        public string Info;
        public int SplitCount;
        public ParaValueSetting(string keyName, string regex, int length, string info, int splitCount)
        {
            KeyName = keyName;
            Regex = regex;
            Length = length;
            Info = info;
            SplitCount = splitCount;
        }
    }
  
    /// <summary>
    /// ParameterWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ParameterWindow : Window
    {
        public Dictionary<string, string> ParaValueResult = new Dictionary<string, string>();

        public Dictionary<string, ParameterGrid> ParaGridMap = new Dictionary<string, ParameterGrid>();

        public void Add(ParameterGrid Para)
        {
            Para.ParaUpdate = new ParameterGrid.ParaUpdateDelegate(ParaUpdated);
            ParameterGrids.Children.Add(Para);
            ParaGridMap.Add(Para.KeyName, Para);
            Para.RegisterOwner(this);
        }

        public void AddRange(List<ParameterGrid> ParaLists)
        {
            foreach (ParameterGrid Para in ParaLists)
            {
                Add(Para);
            }
        }

        public ParameterWindow(Window ParentWindow, string WindowName, List<ParameterGrid> ParaLists)
        {
            InitializeComponent();
            Owner = ParentWindow;
            Title = WindowName;
            AddRange(ParaLists);
            ParaUpdated(null, null);
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            foreach(ParameterGrid pvr in ParameterGrids.Children)
            {
                if (!pvr.IsComplete)
                {
                    MessageBox.Show(this, "还未配置完成！");
                    return;
                }
                else
                {
                    ParaValueResult[pvr.KeyName] = pvr.ParaValue;
                }
            }
            DialogResult = true;
            Close();
        }

        private void ParaUpdated(string Key, string Value)
        {
            foreach (ParameterGrid pg in ParaGridMap.Values)
            {
                if (pg.Dependence == null || pg.Dependence.Count == 0) continue;
                bool FindValue = false;
                foreach(KeyValuePair<string, string[]> dep in pg.Dependence)
                {
                    if (dep.Value.Contains(ParaGridMap[dep.Key].ParaValue))
                    {
                        pg.Show();
                        FindValue = true;
                        break;
                    }
                }
                if (FindValue) continue;
                pg.Hide();
            }
        }
    }
}
