using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace SMTool.UI
{
    /// <summary>
    /// WaitWindow.xaml 的交互逻辑
    /// </summary>
    public partial class WaitWindow : Window
    {

        public delegate bool WaitDelegate(WaitWindow ww, object o);

        public WaitDelegate WaitAction;

        public object ThreadParameter;

        public WaitWindow(Window owner, string title, WaitDelegate waitAction) : this(owner, title, waitAction, null) {

        }

        public WaitWindow(Window owner, string title, WaitDelegate waitAction, object threadParameter)
        {
            InitializeComponent();
            Title = title;
            WaitInfoTextBlock.Text = title;
            WaitAction = waitAction;
            Owner = owner;
            ThreadParameter = threadParameter;
        }

        public void WaitPercent(double percent) => WaitPercent(percent, Brushes.Green);

        public void WaitPercent(double percent, SolidColorBrush foreground) => Dispatcher.Invoke(
                new Action(
                    () =>
                    {
                        WaitProgressBar.Value = percent;
                        WaitProgressBar.Foreground = foreground;
                    }
                )
            );

        public void WaitThread(object o)
        {
            bool WaitResult = WaitAction(this, o);
            WaitPercent(100, WaitResult == false ? Brushes.Red : Brushes.Green);
            Thread.Sleep(500);
            Dispatcher.Invoke(
                new Action(
                    () => {
                        DialogResult = WaitResult;
                        Close();
                    }
                )
            );

        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            Thread WaitThreadHandle = new Thread(WaitThread);
            WaitThreadHandle.Start(ThreadParameter);
        }
    }
}
