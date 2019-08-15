using ISPCore.Connect;
using ISPCore.Packet;
using ISPCore.Util;
using SMTool.Protocol;
using SMTool.Protocol.ShellCmd;
using SMTool.UI;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;

namespace SMTool.Connect
{
    public class SerialConnector : ConnectorBase, IProtocolConnector
    {
        // 私有方法及对象
        public SerialPort serialport;
        public bool IsUSBSerial;

        public CmdManager Cmd;

        public MainWindow Mw;

        #region ShellCmd
        private Queue<ShellCmd> ShellCmdQueue = new Queue<ShellCmd>();

        private static readonly object ShellCmdLock = new object();

        private bool IsExclusiveMode = false;
        public int CmdCount
        {
            get
            {
                return ShellCmdQueue.Count;
            }
        }

        public SendPacketDelegate SendHandle {
            get
            {
                return SendFunction;
            }
        }

        public ReceiveDataDelegate ReceiveHandle { get; set; }

        public bool AddCmd(ShellCmd cmd)
        {
            lock (ShellCmdLock)
            {
                try
                {
                    ICmd newcmd; 
                    switch (Mw.ChipName)
                    {
                        default:
                            newcmd = null;
                            break;
                    }
                    if(!Cmd.Add(newcmd))
                    {
                        return false;
                    }
                    Cmd.Start();
                    return true;
                }
                catch (Exception ex)
                {
                    Log.error("添加命令失败！错误消息：" + ex.Message);
                    return false;
                }
                // TODO
                /*if (IsExclusiveMode) return false;
                ShellCmdQueue.Enqueue(cmd);
                return true;*/
            }
        }

        public bool AddCmd(ShellCmd cmd, string groupID)
        {
            lock (ShellCmdLock)
            {
                if(IsExclusiveMode && ShellCmdQueue.Count > 0 && ShellCmdQueue.Peek().GroupID == groupID)
                {
                    cmd.GroupID = groupID;
                    ShellCmdQueue.Enqueue(cmd);
                    return true;
                }
                else if (!IsExclusiveMode && ShellCmdQueue.Count == 0)
                {
                    cmd.GroupID = groupID;
                    cmd.IsStartCmd = true;
                    ShellCmdQueue.Enqueue(cmd);
                    IsExclusiveMode = true;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public bool AddCmdGroup(ShellCmd[] cmds, string groupID)
        {
            lock (ShellCmdLock)
            {
                // TODO
                foreach (ShellCmd cmd in cmds)
                {
                    try
                    {
                        ICmd newcmd;
                        switch (Mw.ChipName)
                        {
                            default:
                                newcmd = null;
                                break;
                        }
                        if (!Cmd.Add(newcmd))
                        {
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.error("添加命令失败！错误消息：" + ex.Message);
                        return false;
                    }
                }
                Cmd.Start();
                return true;
                /*
                if (IsExclusiveMode || ShellCmdQueue.Count != 0) return false;
                foreach (ShellCmd cmd in cmds)
                {
                    cmd.GroupID = groupID;
                    ShellCmdQueue.Enqueue(cmd);
                }
                ShellCmdQueue.Peek().IsStartCmd = true;
                IsExclusiveMode = true;
                return true;*/
            }
        }

        public void ClearCmd(bool isInExclusiveMode)
        {
            lock (ShellCmdLock)
            {
                IsExclusiveMode = isInExclusiveMode;
                ShellCmdQueue.Clear();
            }
        }
        public void ClearCmd()
        {
            ClearCmd(false);
        }
        #endregion

        public SerialConnector()
            : base(ConnectorType.SerialConnector)
        {
            Cmd = new CmdManager(this);
        }

        public List<byte> ReadBlockBytes = new List<byte>();
        public DateTime ReadByteTime;
        public byte[] ReadBuffer = new byte[0x2000];

        public bool SendFunction(byte[] SendBuffer)
        {
            if (serialport != null && serialport.IsOpen && SendBuffer.Length > 0)
            {
                Log.debug("PSend-> " + Tool.HexToString(SendBuffer).ToUpper());
                serialport.Write(SendBuffer, 0, SendBuffer.Length);
                return true;
            }
            else
            {
                return false;
            }
        }
      
        private int ReadCount;
        // 串口读取线程
        private void SerialReadThread(object sender, SerialDataReceivedEventArgs e)
        {
            // 串口读取事件 每接收一个字节触发一次
            try
            {
                ReadCount = serialport.BytesToRead;
                ReadCount = ReadCount > 8192 ? 8192 : ReadCount;
                serialport.Read(ReadBuffer, 0, ReadCount);
                byte[] recv = new byte[ReadCount];
                Array.Copy(ReadBuffer, recv, ReadCount);
                ReceiveHandle?.Invoke(ReadBuffer.Take(ReadCount).ToArray());
                //Log.debug("R: " + Tool.HexToString(ReadBuffer.Take(ReadCount).ToArray()).ToUpper());
            }
            catch (Exception ex)
            {
                Log.error(ex.Message);
                Log.debug("Rerror: " + Tool.HexToString(ReadBuffer.Take(ReadCount).ToArray()));
            }
        }

        // 串口发送线程
        protected override void WriteThread(object state)
        {
            lock (WriteTimerLock)
            {
                byte[] WriteBytes;
                if (SerialChannel == SerialChannelEnum.ChipConnect)
                {
                     WriteBytes = PACK.ReadyToWrite();
                }
                else
                {
                    lock (ShellCmdLock)
                    {
                        if (ShellCmdQueue.Count > 0 && (!ShellCmdQueue.Peek().IsSent))
                        {
                            WriteBytes = ShellCmdQueue.Peek().CmdBytes;
                            ShellCmdQueue.Peek().IsSent = true;
                        }
                        else
                        {
                            return;
                        }
                    }
                }
                    
                try
                {
                    if (serialport != null && serialport.IsOpen && WriteBytes.Length > 0)
                    {
                        serialport.Write(WriteBytes, 0, WriteBytes.Length);
                        Log.debug("send: " + Tool.HexToString(WriteBytes, " "));
                    }
                }
                catch(Exception ex)
                {
                    Log.error(ex.Message);
                    Disconnect();
                }
            }
        }

        protected override void ReadThread(object state)
        {
            lock (ShellCmdLock)
            {
                if (ShellCmdQueue.Count > 0)
                {
                    ShellCmd CurrentCmd = ShellCmdQueue.Peek();
                    if (CurrentCmd.TryAnalysisReslut())
                    {
                        if (ShellCmdQueue.Count > 0) ShellCmdQueue.Dequeue();
                        if (IsExclusiveMode && ShellCmdQueue.Count == 0)
                        {
                            Log.info("========== 命令组 [" + CurrentCmd.GroupID + "] 执行完成 ==========");
                            Log.info("开始时间: " + CurrentCmd.StartCmdTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                            Log.info("停止时间: " + CurrentCmd.ReadTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                            Log.info("执行时长: " + (((int)(CurrentCmd.ReadTime - CurrentCmd.StartCmdTime).TotalMilliseconds) * 0.001).ToString() + " 秒!");
                            IsExclusiveMode = false;
                        }
                        else if(IsExclusiveMode && ShellCmdQueue.Peek().GroupID == CurrentCmd.GroupID)
                        {
                            ShellCmdQueue.Peek().StartCmdTime = CurrentCmd.StartCmdTime;
                        }
                        else if (IsExclusiveMode)
                        {
                            Log.error("未知情景，退出命令独占状态！");
                            IsExclusiveMode = false;
                        }
                    }
                }
            }
        }

        public override void AutoConnectChip(string devicename)
        {
        }

        public enum SerialChannelEnum
        {
            ChipConnect,
            TestConnect
        }

        public SerialChannelEnum SerialChannel = SerialChannelEnum.TestConnect;

        public override void Connect(object device)
        {
            if (device is SerialPort)
            {
                IsUSBSerial = false;
                serialport = (SerialPort)device;
                try
                {
                    serialport.DataBits = 8;
                    serialport.StopBits = StopBits.One;
                    serialport.Parity = Parity.None;
                    serialport.ReadTimeout = 50;
                    serialport.ReceivedBytesThreshold = 1;
                    serialport.DataReceived +=
                        new SerialDataReceivedEventHandler(SerialReadThread);
                    serialport.ReadBufferSize = 8192;
                    serialport.WriteBufferSize = 8192;
                    PACK.FlushReadBuffer();
                    PACK.FlushWriteBuffer();
                    CMD.ClearTask();
                    serialport.Open();
                    WriteTimer.Change(0, 25);
                    ReadTimer.Change(0, 25);
                    SerialChannel = SerialChannelEnum.ChipConnect;
                    CreateEvent(EventReason.ChipConnected, null);   // 仅串口连接成功
                    return;
                }
                catch(Exception ex)
                {
                    Disconnect();
                    CreateEvent(EventReason.ConnectFailed, ex.Message);
                    return;
                }
            }
            CreateEvent(EventReason.ConnectFailed, "连接的不是串口设备！");
        }

        public void WaitUntilThreadOver(){
            lock (ShellCmdLock)
            {
                Log.info("等待命令结束...");
            }
            lock (WriteTimerLock) {
                serialport.BreakState = false;
                Log.info("等待发送结束...");
            };
            lock (PacketTimerLock) {
                serialport.ReadExisting();
                Log.info("等待接收结束...");
            };
        }

        public override void Disconnect() {
            try
            {
                ClearCmd();
                Cmd.Clear();
                ReadTimer.Change(-1, -1);
                WriteTimer.Change(-1, -1);
                WaitUntilThreadOver();
                serialport.Close();
                CreateEvent(EventReason.Disconnected, null);
            }
            catch (Exception ex)
            {
                CreateEvent(EventReason.Disconnected, ex.Message);
            }
            finally
            {
                PACK.FlushReadBuffer();
                PACK.FlushWriteBuffer();
                CMD.ClearTask();
                serialport = null;
            }
        }

        internal void SerialChangedEvent(DeviceListenerArgs e)
        {
            if(e.DeviceType == DeviceListener.DBT_DEVTYP_PORT)
            {
                if(e.DeviceAction == DeviceListener.DBT_DEVICEREMOVECOMPLETE && serialport!=null && !serialport.IsOpen)
                {
                    Log.warn("串口已经断开！芯片连接即将关闭！");
                    Disconnect();
                } 
                CreateEvent(EventReason.SerialChanged, null);
            }
        }

        protected override void ClearRead()
        {
        }
    }
}