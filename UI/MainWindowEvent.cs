using ISPCore.Connect;
using ISPCore.Util;
using SMTool.Connect;
using System;
using System.Windows;

namespace SMTool.UI
{
    public partial class MainWindow : Window
    {
        public void ChipConnected(string ChipName)
        {
            DownloadButton.IsEnabled = true;
            SerialConnectButton.Content = "断开";
        }

        public void ChipDisConnected(string ChipName)
        {
            DownloadButton.IsEnabled = false;
            SerialConnectButton.Content = "连接";
        }

        public void ConnectorCallBack(ConnectorArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                switch (e.Reason)
                {
                    #region 连接逻辑处理
                    case EventReason.ChipConnected:
                        if (e.Sender is SerialConnector)
                            Connector = Serialcon;
                        else
                            break;
                        ChipConnected(ChipName);
                        Log.info("串口连接成功！");
                        break;
                    case EventReason.ConnectFailed:
                        if(Serialcon.SerialChannel == SerialConnector.SerialChannelEnum.ChipConnect)
                        {
                            Serialcon.SerialChannel = SerialConnector.SerialChannelEnum.TestConnect;
                            Log.warn("芯片连接失败！" + (e.State is string ? (string)e.State : ""));
                        }
                        else
                        {
                            ChipDisConnected(ChipName);
                            Log.warn("串口连接失败！" + (e.State is string ? (string)e.State : ""));
                        }
                        break;
                    case EventReason.Connected:
                        Log.info("芯片连接成功！");
                        break;
                    case EventReason.Disconnected:
                        if(e.State is string)
                            Log.warn("断连时发生异常：" + (string)e.State);
                        if (SerialConnectButton.Content.ToString() == "正在连接")
                            Log.info("停止连接芯片！");
                        else
                            Log.info("芯片断开连接！");
                        ChipDisConnected(ChipName);
                        Connector = null;
                        Serialcon.SerialChannel = SerialConnector.SerialChannelEnum.TestConnect;
                        break;
                    #endregion
                }
            }));
        }
    }
}
