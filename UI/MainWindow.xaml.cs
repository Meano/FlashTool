using ISPCore.Connect;
using ISPCore.Packet;
using ISPCore.Util;
using Microsoft.Win32;
using SMTool.Connect;
using SMTool.Protocol.FlashCmd;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using static SMTool.Protocol.FlashCmd.FlashCmdTemplate;

namespace SMTool.UI
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            InitializeLog();
            InitializeSerialPort();
            InitializeConnector();
            InitializeFlashCmd();
            DownloadButton.IsEnabled = false;
            Title = Title + " V" + App.BuildVersion();
        }

        #region Log

        public DispatcherTimer UITimer;
        public void InitializeLog()
        {
            Log.InitializeLog();
            Log.Event += new LogHandler(Logui);
            UITimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(200),
                DispatcherPriority.Normal,
                UITimerCallBack,
                Dispatcher
            );
        }

        private void UITimerCallBack(object sender, EventArgs e)
        {
            if (LogNewlines > 0)
            {
                LogNewlines = 0;
                LogRichTextBox.ScrollToEnd();
            }
        }

        public bool IsLogEndWithNewLine = true;

        private static object LogLock = new object();

        public int LogNewlines = 0;
        public void Logui(LogArgs a)
        {
            Dispatcher.BeginInvoke(new Action<LogArgs>((LogArgs arg) =>
            {
                SolidColorBrush messagecolor = new SolidColorBrush();
                switch (arg.type)
                {
                    case LogType.Debug:
                        messagecolor = Brushes.Purple;
                        break;
                    case LogType.Error:
                        messagecolor = Brushes.Red;
                        break;
                    case LogType.Fatal:
                        messagecolor = Brushes.DarkRed;
                        break;
                    case LogType.Warn:
                        messagecolor = Brushes.DarkGoldenrod;
                        break;
                    case LogType.Info:
                        messagecolor = Brushes.Black;
                        break;
                    case LogType.Shell:
                        messagecolor = Brushes.DarkSlateGray;
                        break;
                    default:
                        break;
                }

                Run logmessage = new Run(arg.message)
                {
                    Foreground = messagecolor,
                };

                string LogHeader = "[" + DateTime.Now.ToLongTimeString() + " " + Enum.GetName(typeof(LogType), arg.type).ToUpper() + " ]: ";

                logmessage.Text = LogHeader + logmessage.Text;
                if (LogParagraph.Inlines.Count > 100)
                {
                    for (int i = 0; i < 50; i++)
                    {
                        LogParagraph.Inlines.Remove(LogParagraph.Inlines.FirstInline);
                    }
                }
                if (LogParagraph.Inlines.Count > 0)
                {
                    ((Run)LogParagraph.Inlines.LastInline).Text += '\n';
                }
                LogParagraph.Inlines.Add(logmessage);
                LogNewlines++;
               
            }), a);
        }

        private void LogClearButton_Click(object sender, RoutedEventArgs e)
        {
            LogParagraph.Inlines.Clear();
        }
        #endregion

        #region 串口

        // 串口初始化
        private void InitializeSerialPort()
        {
            SerialPortFind(null, null);
        }

        // 查找可用串口
        private bool FindAvailablePorts()
        {
            bool IsPortChanged = false;
            string[] AvaliablePorts = SerialPort.GetPortNames();
            Array.Sort(AvaliablePorts);
            if (AvaliablePorts.Count() == SerialPortCombo.Items.Count)
            {
                foreach (string spname in AvaliablePorts)
                {
                    if (!SerialPortCombo.Items.Contains(spname))
                    {
                        IsPortChanged = true;
                        break;
                    }
                }
            }
            else
            {
                IsPortChanged = true;
            }
            if (IsPortChanged)
            {
                SerialPortCombo.Items.Clear();       // 清除现有列表
                foreach (string spname in AvaliablePorts)
                {
                    SerialPortCombo.Items.Add(spname);
                }
            }
            return IsPortChanged;
        }

        private void SerialPortFind(object sender, EventArgs e)
        {
            if (FindAvailablePorts())
            {
                SerialPortCombo.SelectedIndex = 0;
                Log.info("当前系统有可用端口" + SerialPortCombo.Items.Count + "个！");
            }
        }

        public bool IsUSBSerial;
        public SerialConnector Serialcon;
        public IConnector Connector;

        public DeviceListener DeviceMessage;
        public void InitializeConnector()
        {
            DeviceMessage = new DeviceListener();
            SourceInitialized += DeviceMessage.RegisterListener;

            Serialcon = new SerialConnector
            {
                Mw = this
            };
            Serialcon.Event += new ConnectorHandler(ConnectorCallBack);
            DeviceMessage.Event += Serialcon.SerialChangedEvent;
        }

        public string ChipName;

        private void SerialConnectButton_Click(object sender, RoutedEventArgs e)
        {
            object device = null;
            IConnector connector = Serialcon;
            if (!SerialPortCombo.Text.Trim().StartsWith("COM"))
            {
                Log.warn("没有可用串口！");
                return;
            }
            if (SerialConnectButton.Content.ToString() == "连接")
            {
                string SerialComName = SerialPortCombo.Text.Trim();
                GetSerialInfo SerialInfo = GetSerialInfo.Get(SerialComName);
                device = new SerialPort(SerialComName, 768000);
                SerialConnectButton.Content = "正在连接";
                Log.info("正在连接......");
                connector.Connect(device);
            }
            else
            {
                connector.Disconnect();
            }
        }
        #endregion

        private void InitializeFlashCmd()
        {
            FlashCmd.RegisterCMD(FlashReadCmd);
            FlashCmd.RegisterCMD(FlashWriteCmd);
            FlashCmd.RegisterCMD(FlashEraseCmd);
        }

        static List<byte> FlashFileList = null;
        private FlashCmdTemplate FlashReadCmd =
            new FlashCmdTemplate(
                "Read",
                0x00,
                new PacketAnalysisDelegate((FlashCmdResult result) =>
                {
                    lock (FlashFileList)
                    {
                        if (!result.IsSuccess)
                        {
                            Log.error("读取失败！");
                            SaveBin(FlashReadAddress, FlashFileList);
                            return false;
                        }
                        if (FlashFileList == null)
                        {
                            Log.warn("没有要填充的数据List！");
                            return false;
                        }
                        uint CurrentFileSize = (uint)FlashFileList.Count();
                        uint CurrentCmdSize = result.ResponsePara - FlashReadAddress;
                        if (CurrentFileSize != CurrentCmdSize)
                        {
                            Log.warn("读取地址有误: " + CurrentFileSize.ToString("X08") + ":" + CurrentCmdSize.ToString("X08"));
                            return false;
                        }
                        FlashFileList.AddRange(result.Payload);
                        Log.info("当前地址: " + result.ResponsePara.ToString("X08") + " 读取进度: " + (((double)FlashFileList.Count()) / ((double)(FlashFileLength * 10.24))).ToString("F2"));
                        if (FlashFileList.Count() >= (FlashFileLength * 1024))
                        {
                            SaveBin(FlashReadAddress, FlashFileList);
                            FlashFileList = null;
                        }
                        return true;
                    }
                })
            )
            {
                RetryCount = 10,
                ThreadPeroid = 10,
                RequestTimeout = 500,
                ResponseTimeout = 1000,
            };
        public static void SaveBin(uint StartAddress, List<byte> SaveList)
        {
            uint EndAddress = (uint)(StartAddress + SaveList.Count());
            string BinFileName = "FlashRead-0x" + StartAddress.ToString("X08") + "-0x" + EndAddress.ToString("X08") + "-" + DateTime.Now.ToFileTime() + ".bin";
            FileStream fs = new FileStream(BinFileName, FileMode.OpenOrCreate);
            fs.Seek(0, SeekOrigin.Begin);
            fs.Write(FlashFileList.ToArray(), 0, FlashFileList.Count());
            fs.Close();
            Log.info("文件成功保存在: " + BinFileName);
        }

        private FlashCmdTemplate FlashWriteCmd =
            new FlashCmdTemplate(
                "Write",
                0x01,
                new PacketAnalysisDelegate((FlashCmdResult result) =>
                {
                        if (!result.IsSuccess)
                        {
                            Log.error("写入失败！");
                            return false;
                        }

                        Log.info("当前地址: " + result.ResponsePara.ToString("X08") + " 写入进度: " + (((double)result.ResponsePara - FlashWriteAddress + 4096) * 100.0 / fw.BinFileByte.Length).ToString("F2") + "%");
                        return true;
                })
            )
            {
                RetryCount = 10,
                ThreadPeroid = 10,
                RequestTimeout = 500,
                ResponseTimeout = 1000,
            };

        private FlashCmdTemplate FlashEraseCmd =
           new FlashCmdTemplate(
               "Erase",
               0x02,
               new PacketAnalysisDelegate((FlashCmdResult result) =>
               {
                   if (!result.IsSuccess)
                   {
                       Log.error("擦除失败！");
                       return false;
                   }
                   Log.info("从 0x" + result.ResponsePara.ToString("X08") + "到 0x" + (FlashEraseAddress + FlashEraseLength * 1024).ToString("X08") + " 擦除成功!写入进度" + (((double)result.ResponsePara - FlashWriteAddress + 4096) * 100.0 / fw.BinFileByte.Length).ToString("F2") + "%");
                   return true;
               })
           )
           {
               RetryCount = 10,
               ThreadPeroid = 10,
               RequestTimeout = 500,
               ResponseTimeout = 5000,
           };

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            fw.IsCheckSection = false;
            if (!fw.FirmwareReady)
            {
                Log.error("还未配置可下载固件，下载中止！");
                return;
            }

            if(MessageBox.Show(this, "将下载0x" + fw.BinFileByte.Length.ToString("X08") + "数据到" + FlashWriteAddress.ToString("X08"), "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                Log.warn("取消下载！");
                return;
            }

            for(uint offset = 0; offset < fw.BinFileByte.Length; offset += 4096)
            {
                byte[] Write4Kbyte = new byte[4096];
                Array.Copy(fw.BinFileByte, offset, Write4Kbyte, 0, 4096);
                bool IsOnlyClear = true;
                for (uint i = 0; i < 4096; i++)
                {
                    if (Write4Kbyte[i] != 0xFF)
                    {
                        IsOnlyClear = false;
                        break;
                    }
                }
                FlashCmd cmd; 
                if (IsOnlyClear)
                {
                    byte[] EraseLength = BitConverter.GetBytes(4 / 4);
                    Array.Reverse(EraseLength);
                    cmd = new FlashCmd("Erase", (offset + FlashWriteAddress), EraseLength);
                }
                else
                {
                    cmd = new FlashCmd("Write", (offset + FlashWriteAddress), Write4Kbyte);
                }
          
                Serialcon.Cmd.Add(cmd);
            }

            Serialcon.Cmd.Start();
        }

        static WPacketFWHeader fw = new WPacketFWHeader();
        static uint FlashWriteAddress = 0;
        private void FileSelectButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog FWOpenFile = new OpenFileDialog()
            {
                Title = "请选择固件文件",
                InitialDirectory = "",
                Filter =
               "二进制bin文件|*.bin|十六进制hex文件|*.hex",
                ValidateNames = true,
                CheckPathExists = true,
                CheckFileExists = true,
                Multiselect = false,
            };

            if (FWOpenFile.ShowDialog() != true)
            {
                Log.warn("未选择文件，取消下载！");
                return;
            }

            List<ParameterGrid> pgList = new List<ParameterGrid>();

            pgList.Add(new ParameterGrid(
                "Address",
                "存放地址",
                @"^[0-9A-Fa-f]*$",
                8,
                "输入四字节的存放地址",
                FlashWriteAddress.ToString("X08"),
                null
                )
            );

            ParameterWindow pw = new ParameterWindow(this, "配置固件存放地址", pgList);

            if(pw.ShowDialog() != true)
            {
                Log.warn("未配置存放地址，取消下载！");
                return;
            }

            FlashWriteAddress = uint.Parse(pw.ParaValueResult["Address"], NumberStyles.HexNumber);

            string FileRealName = FWOpenFile.SafeFileName;
            string FileFullName = FWOpenFile.FileName;

            string FileName = FileRealName.ToLower();

            fw.IsCheckSection = false;
            fw.FirmwareName = FileFullName;
            FileLabel.Content = FileRealName;
        }

        string CheckString = "";
        private void CheckSumButton_Click(object sender, RoutedEventArgs e)
        {
            List<ParameterGrid> pgList = new List<ParameterGrid>();

            pgList.Add(new ParameterGrid(
                "CheckSum",
                "校验内容",
                @"^[0-9A-Fa-f ]*$",
                0,
                "输入需要检验的内容",
                CheckString,
                null
                )
            );

            ParameterWindow pw = new ParameterWindow(this, "计算校验和", pgList);
            if (pw.ShowDialog() != true)
            {
                Log.warn("取消校验！");
                return;
            }
            CheckString = pw.ParaValueResult["CheckSum"].Trim();
            byte[] CheckBytes = Tool.StringToHex(CheckString);
            byte sum = 0;
            ushort intelsum = 0x0000;
            for(int i = 0; i < CheckBytes.Length; i++)
            {
                sum += CheckBytes[i];
                if(i % 2 == 1)
                {
                    intelsum += BitConverter.ToUInt16(CheckBytes, i - 1);
                }
            }
            Log.info("========== Sum Report ==========");
            Log.info("Sum : " + sum.ToString("X02"));
            Log.info("PPIDSum : " + ((byte)(0xA0 - sum)).ToString("X02"));
            Log.info("InvSum : " + ((byte)~sum).ToString("X02"));
            Log.info("UShort Sum : " + intelsum.ToString("X04"));
            Log.info("Intel Sum : " + ((ushort)(0xBABA - intelsum)).ToString("X04"));
        }

        static uint FlashReadAddress = 0;
        static int FlashFileLength = 64;
        private void FlashReadButton_Click(object sender, RoutedEventArgs e)
        {
            List<ParameterGrid> pgList = new List<ParameterGrid>();

            pgList.Add(new ParameterGrid(
                "Address",
                "读取基址",
                @"^[0-9A-Fa-f]*$",
                8,
                "输入读取Flash基址",
                FlashReadAddress.ToString("X08"),
                null
                )
            );
            pgList.Add(new ParameterGrid(
               "Length",
               "读取长度(KB)",
               @"^[0-9]*$",
               0,
               "输入读取Flash长度",
               FlashFileLength.ToString(),
               null
               )
           );

            ParameterWindow pw = new ParameterWindow(this, "读取Flash配置", pgList);
            if (pw.ShowDialog() != true)
            {
                Log.warn("取消读取！");
                return;
            }
            FlashReadAddress = uint.Parse(pw.ParaValueResult["Address"], NumberStyles.HexNumber);
            int ReadLength = int.Parse(pw.ParaValueResult["Length"]);
            ReadLength = (ReadLength / 4 + (ReadLength % 4 == 0 ? 0 : 1)) * 4;
            FlashFileLength = ReadLength;
            FlashFileList = new List<byte>();
            
            MessageBox.Show(this, "读取文件从 0x" + FlashReadAddress.ToString("X08") + "到 0x" + (FlashReadAddress + ReadLength * 1024).ToString("X08"));
            for (uint offset = 0; offset < (FlashFileLength * 1024); offset += 4096)
            {
                var cmd = new FlashCmd("Read", FlashReadAddress + offset, null);
                Serialcon.Cmd.Add(cmd);
            }
            Serialcon.Cmd.Start();
        }

        static uint FlashEraseAddress = 0;
        static int FlashEraseLength = 64;
        private void FlashEraseButton_Click(object sender, RoutedEventArgs e)
        {
            List<ParameterGrid> pgList = new List<ParameterGrid>();

            pgList.Add(new ParameterGrid(
                "Address",
                "擦除基址",
                @"^[0-9A-Fa-f]*$",
                8,
                "输入擦除Flash基址",
                FlashEraseAddress.ToString("X08"),
                null
                )
            );
            pgList.Add(new ParameterGrid(
                "Length",
                "擦除长度(KB)",
                @"^[0-9]*$",
                0,
                "输入擦除Flash长度",
                FlashEraseLength.ToString(),
                null
                )
            );

            ParameterWindow pw = new ParameterWindow(this, "擦除Flash配置", pgList);
            if (pw.ShowDialog() != true)
            {
                Log.warn("取消擦除！");
                return;
            }
            FlashEraseAddress = uint.Parse(pw.ParaValueResult["Address"], NumberStyles.HexNumber);
            FlashEraseLength = int.Parse(pw.ParaValueResult["Length"]);
            FlashEraseLength = (FlashEraseLength / 4 + (FlashEraseLength % 4 == 0 ? 0 : 1)) * 4;
            FlashFileList = new List<byte>();

            MessageBox.Show(this, "擦除从 0x" + FlashEraseAddress.ToString("X08") + "到 0x" + (FlashEraseAddress + FlashEraseLength * 1024).ToString("X08"));
            byte[] EraseLength = BitConverter.GetBytes(FlashEraseLength / 4);
            Array.Reverse(EraseLength);
            var cmd = new FlashCmd("Erase", FlashEraseAddress, EraseLength);
            Serialcon.Cmd.Add(cmd);
            Serialcon.Cmd.Start();
        }

        Dictionary<uint, string> ObjTypes = new Dictionary<uint, string>()
        {
            { 0x00049201, "PPID" },
            { 0x00042901, "FanCtrlOvrd" },
            { 0x00042A01, "ChassisPolicy" },
            { 0x00261801, "Service Tag" },
            { 0x00061701, "Asset Tag" },
            { 0x000E3001, "ProductName" },
            { 0x000E3101, "Sku" },
            { 0x000E7801, "System Map" },
            { 0x00250303, "FirstPowerOnDate" },
            { 0x00250203, "MfgDate" },
            { 0x00062B01, "May Man Mode" },
            { 0x00000102, "May Man Mode1" },
            { 0x00044501, "AcPwrRcvry" },
            { 0x00047801, "WakeOnLan5" }
        };

        public void WriteAnalysisLine(StreamWriter s, string ObjType, uint ObjHeader, uint ObjSubType, string ObjSubName, uint length, byte[] ObjData, byte[] OrigData)
        {
            string line =
                ObjType + ",\"" +
                ObjHeader.ToString("X08") + "\",\"" +
                ObjSubType.ToString("X08") + "\",\"" +
                ObjSubName.PadRight(16, ' ') + "\",\"" +
                length.ToString("D03") + "\",\"" +
                Tool.HexToString(ObjData) + "\", [";
            foreach(byte obyte in ObjData)
            {
                line += (obyte < 127 && obyte >= 0x30) ? Encoding.ASCII.GetString(new byte[1] { obyte }) : ".";
            }
            line += "],";
            line += Tool.HexToString(OrigData);
            s.Write(line + ",\n");
        }

        struct AnalysisType
        {
            public string Name;
            public uint Head;
            public uint Type;
            public string TypeName;
            public uint Length;
            public byte[] Data;
            public uint Offset;
            public byte[] OrigData;
            public void SetData(string name, uint head, uint type, string typename, uint length, byte[] data, byte[] origData)
            {
                Name = name;
                Head = head;
                Type = type;
                TypeName = typename;
                Length = length;
                Data = data;
                OrigData = origData;
            }
        }

        List<AnalysisType> AnalysisList = new List<AnalysisType>(); 
        private void AnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog loganalysisdialog = new OpenFileDialog
            {
                Title = "请选择要分析的文件",
                InitialDirectory = "",
                Filter = "二进制文件(*.bin)" + "|*.bin",
                ValidateNames = true,
                CheckPathExists = true,
                CheckFileExists = true,
                Multiselect = false
            };

            if (loganalysisdialog.ShowDialog(this) != true)
            {
                return;
            }
            Log.info("=========== Start Analysis ===========");
            string AnaReportFileName = loganalysisdialog.FileName.Replace(".bin", ".RPT.csv");
            StreamWriter AnaFile = new StreamWriter(AnaReportFileName);
            byte[] AnaBytes = File.ReadAllBytes(loganalysisdialog.FileName);
            uint lastObjEnd = 0x00000000;
            uint lastObjStart = 0x00000000;
            AnalysisList.Clear();
            for(uint address = 0; address < AnaBytes.Length;)
            {
                if(AnaBytes.Length - address < 8)
                {
                    break;
                }
                uint Header = BitConverter.ToUInt32(AnaBytes, (int)address);
                uint SubType = BitConverter.ToUInt32(AnaBytes, (int)address + 4);
                bool IsKnownHeader = true;
                AnalysisType AnalysisObj = new AnalysisType();
                int Length;
                switch (Header)
                {
                    case 0x52415644:
                        AnaFile.WriteLine("========== DVAR File ===========");
                        AnaFile.WriteLine("Name,Head,Type,TypeName,Length,Data,ASCII");
                        address += 9;
                        break;
                    case 0xF8FBFDFA:
                    case 0xF8FBFDAA:
                        Length = 0xFF - AnaBytes[address + 7];
                        SubType &= 0x00FFFFFF;
                        SubType = 0x00FFFFFF - SubType;
                        AnalysisObj.SetData(
                            "OPTION1",
                            Header,
                            SubType,
                            ObjTypes.ContainsKey(SubType) ? ObjTypes[SubType] : "",
                            (uint)Length,
                            AnaBytes.Skip((int)address + 8).Take(Length).ToArray(),
                            AnaBytes.Skip((int)address).Take(Length + 8).ToArray()
                            );
                        lastObjStart = address;
                        address += (uint)(8 + Length);
                        break;
                    case 0xFFF8FDFA:
                    case 0xFFF8FDAA:
                    case 0xF8FFFDFA:
                    case 0xF8FFFDAA:
                        Length = 0xFF - AnaBytes[address + 6];
                        SubType &= 0x0000FFFF;
                        SubType = 0x0000FFFF - SubType;
                        AnalysisObj.SetData(
                            "OPTION2",
                            Header,
                            SubType,
                            ObjTypes.ContainsKey(SubType) ? ObjTypes[SubType] : "",
                            (uint)Length,
                            AnaBytes.Skip((int)address + 7).Take(Length).ToArray(),
                            AnaBytes.Skip((int)address).Take(Length + 7).ToArray()
                            );
                        lastObjStart = address;
                        address += (uint)(7 + Length);
                        break;
                    case 0xF8FAFDAA:
                    case 0xF8FAFDFA:
                        Length = 0xFFFF - (AnaBytes[address + 7] | AnaBytes[address + 8] << 8);
                        SubType &= 0x00FFFFFF;
                        SubType = 0x00FFFFFF - SubType;
                        AnalysisObj.SetData(
                            "SYSMAP3",
                            Header,
                            SubType,
                            ObjTypes.ContainsKey(SubType) ? ObjTypes[SubType] : "",
                            (uint)Length,
                            AnaBytes.Skip((int)address + 9).Take(Length).ToArray(),
                            AnaBytes.Skip((int)address).Take(Length + 9).ToArray()
                            );
                        lastObjStart = address;
                        address += (uint)(9 + Length);
                        break;
                    case 0xF8FFF9FA:
                    case 0xF8FFF9AA:
                        Length =  (0xFF - AnaBytes[address + 22]) + 16;
                        SubType &= 0x000000FF;
                        SubType |= (uint)(AnaBytes[address + 21] << 8);
                        AnalysisObj.SetData(
                           "NODETY0",
                           Header,
                           SubType,
                           ObjTypes.ContainsKey(SubType) ? ObjTypes[SubType] : "",
                           (uint)Length,
                           AnaBytes.Skip((int)address + 5).Take(16).Concat(AnaBytes.Skip((int)address + 5 + 16 + 2).Take(Length - 16)).ToArray(),
                           AnaBytes.Skip((int)address).Take(Length + 5).ToArray()
                           );
                        lastObjStart = address;
                        address += (uint)(7 + Length);
                        break;
                    case 0xF8FBF9FA:
                        Length = (0xFF - AnaBytes[address + 23]) + 16;
                        SubType &= 0x000000FF;
                        SubType |= (uint)(AnaBytes[address + 21] << 8);
                        SubType |= (uint)(AnaBytes[address + 22] << 16);
                        AnalysisObj.SetData(
                           "NODETY1",
                           Header,
                           SubType,
                           ObjTypes.ContainsKey(SubType) ? ObjTypes[SubType] : "",
                           (uint)Length,
                           AnaBytes.Skip((int)address + 5).Take(16).Concat(AnaBytes.Skip((int)address + 5 + 16 + 3).Take(Length - 16)).ToArray(),
                           AnaBytes.Skip((int)address).Take(Length + 5 + 3).ToArray()
                           );
                        lastObjStart = address;
                        address += (uint)(8 + Length);
                        break;
                    default:
                        IsKnownHeader = false;
                        break;
                }
                if (IsKnownHeader)
                {
                    if (lastObjEnd != lastObjStart)
                    {
                        Header = BitConverter.ToUInt32(AnaBytes, (int)lastObjEnd);
                        SubType = BitConverter.ToUInt32(AnaBytes, (int)lastObjEnd + 4);
                        WriteAnalysisLine(AnaFile, "UNKNOW0", Header, SubType, "", (uint)(lastObjStart - lastObjEnd), AnaBytes.Skip((int)lastObjEnd).Take((int)(lastObjStart - lastObjEnd)).ToArray(), AnaBytes.Skip((int)lastObjEnd).Take((int)(lastObjStart - lastObjEnd)).ToArray());
                    }
                    if (AnalysisObj.Name != null)
                    {
                        AnalysisObj.Offset = lastObjStart;
                        if ((AnalysisObj.Head & 0xFF) == 0xFA)
                            AnalysisList.Add(AnalysisObj);
                        WriteAnalysisLine(AnaFile, AnalysisObj.Name, AnalysisObj.Head, AnalysisObj.Type, AnalysisObj.TypeName, AnalysisObj.Length, AnalysisObj.Data, AnalysisObj.OrigData);
                    }
                    lastObjEnd = address;
                }
                else
                {
                    address++;
                }
            }
            AnaFile.Close();
            AnaReportFileName = loganalysisdialog.FileName.Replace(".bin", ".SRT.csv");
            AnaFile = new StreamWriter(AnaReportFileName);
            AnalysisList.Sort((a, b) => {
                int typeResult = a.Type.CompareTo(b.Type);
                if(typeResult == 0)
                {
                    return a.Offset.CompareTo(b.Offset);
                }
                else
                {
                    return typeResult;
                }
            });
            for (int i = 0; i < AnalysisList.Count(); i++)
            {
                AnalysisType AnalysisObj = AnalysisList[i];
                WriteAnalysisLine(AnaFile, AnalysisObj.Name, AnalysisObj.Head, AnalysisObj.Type, AnalysisObj.TypeName, AnalysisObj.Length, AnalysisObj.Data, AnalysisObj.OrigData);
            }
            AnaFile.Close();
            Log.info("=========== Ended Analysis ===========");
        }
    }
}
