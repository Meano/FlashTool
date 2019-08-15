using ISPCore.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SMTool.Protocol.ShellCmd
{
    public class ShellCmd
    {
        public string GroupID { get; set; }
        public bool IsStartCmd { get; set; }

        public CmdStatus Status {
            get
            {
                if (IsSent && IsRead)
                {
                    return CmdStatus.Responsed;
                }
                else if (IsSent && (!IsRead))
                {
                    return CmdStatus.Requested;
                }
                else
                {
                    return CmdStatus.Ready;
                }
            }
        }

        public ShellCmd(string cmd) : this(cmd, 0)
        {

        }

        public ShellCmd(string cmd, int timeoutms)
        {
            GroupID = "";
            IsStartCmd = false;
       
            Cmd = cmd;
            Timeout = timeoutms;
            ResultByte = new List<byte>();
            ResultStringList = new List<string>();
            ResultByteList = new List<byte[]>();
        }

        public DateTime SendTime;

        public DateTime StartCmdTime;

        public int Timeout;

        // 命令是否已发出
        public bool IsSent
        {
            get
            {
                return IsSent_;
            }
            set
            {
                if (value && (!IsSent_))
                {
                    SendTime = DateTime.Now;
                    if(IsStartCmd)
                    {
                        StartCmdTime = SendTime;
                    }
                }
                IsSent_ = value;
            }
        }
        private bool IsSent_ = false;

        // 获取发送Byte数组
        public byte[] CmdBytes
        {
            get
            {
                return Encoding.ASCII.GetBytes(Cmd + '\n');
            }
        }

        // 命令
        public string Cmd;

        public string CmdKey
        {
            get
            {
                string cmdKey = "";
                string[] cmdItems = Cmd.Split(' ');
                if(cmdItems.Length > 0)
                {
                    cmdKey += cmdItems[0];
                    foreach(string cmdItem in cmdItems)
                    {
                        if (cmdItem.StartsWith("-"))
                        {
                            cmdKey += " " + cmdItem;
                        }
                    }
                }
                return cmdKey.ToLower();
            }
        }

        public List<string> CmdData
        {
            get
            {
                List<string> cmdData = new List<string>();
                string[] cmdItems = Cmd.Split(' ');
                if (cmdItems.Length > 0)
                {
                    foreach (string cmdItem in cmdItems)
                    {
                        if (!cmdItem.StartsWith("-"))
                        {
                            cmdData.Add(cmdItem);
                        }
                    }
                    cmdData.RemoveAt(0);
                }
                return cmdData;
            }
        }

        public bool IsRead { get; set; } = false;

        public DateTime ReadTime;
        private int AsciiCount = 0;
        public void SaveResult(byte ReadByte)
        {
            ResultByte.Add(ReadByte);
            if (RawCmdKey.Contains(CmdKey))
            {
                if (ReadByte <= 0x7f) AsciiCount++;
                else AsciiCount = 0;
                if (ReadByte == '\n' && AsciiCount >= 3 && AsciiCount == ResultByte.Count)
                {
                    ResultStringList.Add(ResultString.Replace("\r", ""));
                    Log.shell(ResultString.Replace("\r", ""));
                    ResultByte.Clear();
                    AsciiCount = 0;
                }
                else if (ResultString.EndsWith("TS->") && ResultByte.Count > 4)
                {
                    ResultByteList.Add(ResultByte.Take(ResultByte.Count - 4).ToArray());
                    ResultStringList.Add("RawData Saved\n");
                    ResultByte.RemoveRange(0, ResultByte.Count - 4);
                    Log.shell(ResultStringList.Last());
                    AsciiCount = 0;
                }
            }
            else if (ReadByte == '\n')
            {
                ResultStringList.Add(ResultString);
                Log.shell(ResultString.Replace("\r", ""));
                ResultByte.Clear();
            }
            ReadTime = DateTime.Now;
        }

        public bool TryAnalysisReslut()
        {
            // 还没发送
            if (!IsSent)
            {
                return false;
            }
            if ((DateTime.Now - SendTime).TotalMilliseconds > (Timeout != 0 ? Timeout : 3000))
            {
                if (CmdActions.ContainsKey(CmdKey))
                {
                    ReadTime = DateTime.Now;
                    Log.debug("告知" + CmdKey + "处理函数超时！");
                    CmdActions[CmdKey](this);
                }
                return true;
            }
            // 正确获得结果
            if (ResultString.StartsWith("TS->")) {
                ResultStringList.Add(ResultString);
                Log.shell(ResultString.Replace("\r", ""));
                ResultByte.Clear();
                IsRead = true;
                if (CmdActions.ContainsKey(CmdKey))
                {
                    Log.debug("执行" + CmdKey + "的结果分析");
                    CmdActions[CmdKey](this);
                }
                return true;
            }
            return false;
        }

        public static Dictionary<string, Action<ShellCmd>> CmdActions = new Dictionary<string, Action<ShellCmd>>();
        public static List<string> RawCmdKey = new List<string>();

        public List<byte> ResultByte;
        public string ResultString
        {
            get
            {
                return Encoding.ASCII.GetString(ResultByte.ToArray());
            }
        }

        public List<string> ResultStringList;
        public List<byte[]> ResultByteList;
    }
}
