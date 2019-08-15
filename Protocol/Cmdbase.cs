using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SMTool.Protocol
{
    public enum CmdStatus
    {
        Ready,
        Requesting,
        Requested,
        Responsing,
        Responsed,
        End
    }

    public interface ICmd
    {
        CmdStatus Status { get; }

        bool IsReapteCmd { get; set; }

        void Send(PacketManager PacketHandle);

        void SendTimeout();

        void ReceiveTimeout();

        int ThreadPeriod { get; }

        int RequestTimeout { get; }

        int ReponseTimeout { get; }

        DateTime RequestedTime { get; set; }

        DateTime ResponsedTime { get; set; }

        bool IsSuccess { get; }
        bool IsCmdStop { get; set; }

        void SyncExcute(PacketManager packetHandle);
    }

    public abstract class CmdTemplate
    {
        public string Name;
        public string[] Aliases = new string[0];
        public int ThreadPeroid = 50;
        public int RequestTimeout = 1000;
        public int ResponseTimeout;
        public int RetryCount = 0;

        #region Event
        public Action<object> CmdSentEvent;

        public Action<object> CmdRetryEvent;

        public Action<object> CmdEndEvent;
        #endregion

        public CmdTemplate(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    public abstract class CmdResult
    {
        public ICmd RequestCmd;

        public bool IsEnd { get; protected set; }

        public bool IsSuccess { get; protected set; }

        public byte[] Packet { get; protected set; }

        public void RespondFailed(CmdStatus status)
        {
            switch (status)
            {
                case CmdStatus.Ready:
                case CmdStatus.Requesting:
                    FailReson = "RequestTimeout";
                    break;
                case CmdStatus.Responsing:
                case CmdStatus.Requested:
                    FailReson = "ResponseTimeout";
                    break;
                case CmdStatus.Responsed:
                    FailReson = "ResponseFailed";
                    break;
            }
            IsSuccess = false;
            IsEnd = true;
        }

        public string FailReson { get; protected set; } = "";

        public string FailMessage => FailMessages.ContainsKey(FailReson) ? FailMessages[FailReson] : "Unknow Fail Reason: " + FailReson;

        protected Dictionary<string, string> FailMessages = new Dictionary<string, string>
        {
            {"RequestTimeout"  , "命令数据请求失败！请检查数据链路！" },
            {"ResponseTimeout" , "命令请求回应超时！" },
            {"ResponseFailed"  , "命令请求回应数据校验错误！" },
        };

        public void Respond(byte[] packet)
        {
            Packet = packet;
            IsEnd = true;
        }

        public void CmdFailed(string Reason)
        {
            FailReson = Reason;
            IsSuccess = false;
        }

        public void CmdSucceed()
        {
            IsSuccess = true;
        }

        public CmdResult()
        {
            IsSuccess = false;
        }
    }

    public abstract class Cmdbase<Template, Result> : ICmd where Template : CmdTemplate where Result : CmdResult, new()
    {
        #region Interface
        public CmdStatus Status { get; protected set; }

        public bool IsReapteCmd { get; set; }

        public bool ResponseResult { get; set; }

        public int ThreadPeriod => Cmd.ThreadPeroid;

        public int RequestTimeout => Cmd.RequestTimeout;

        public int ReponseTimeout => Cmd.ResponseTimeout;

        public DateTime RequestedTime { get; set; }

        public DateTime ResponsedTime { get; set; }

        public bool IsSuccess => CmdResult.IsSuccess;

        #endregion

        #region Event

        public Action<Result> CmdSentEvent { get; set; }

        public Action<Result> CmdRetryEvent { get; set; }

        public Action<Result> CmdEndEvent { get; set; }

        #endregion

        #region CmdDictionary
        protected static Dictionary<string, Template> CmdDictionary = new Dictionary<string, Template>();

        public static bool RegisterCMD(Template cmdTemplet)
        {
            CmdDictionary.Add(cmdTemplet.Name, cmdTemplet);
            foreach (string alias in cmdTemplet.Aliases)
            {
                CmdDictionary.Add(alias, cmdTemplet);
            }
            return false;
        }
        #endregion

        #region Template
        protected Template Cmd;

        private string Alias = null;

        private int RetryCount;

        public string CmdName
        {
            get
            {
                return Alias ?? Cmd.Name;
            }
            set
            {
                if (value != Cmd.Name)
                {
                    if (Cmd.Aliases.Contains(value))
                    {
                        Alias = value;
                    }
                }
            }
        }

        public string CmdIndexName
        {
            get
            {
                return Cmd.Name;
            }
        }
        #endregion

        #region Result
        public Result CmdResult { get; protected set; } = new Result();
        #endregion

        #region Function
        public Cmdbase(string CmdName)
        {
            CmdResult.RequestCmd = this;
            if (CmdName == null) return;
            Cmd = CmdDictionary[CmdName] ?? throw new ArgumentNullException(nameof(Cmd));
            this.CmdName = CmdName;

            CmdEndEvent += CmdEnd;
            CmdSentEvent += CmdSent;
            CmdRetryEvent += CmdRetry;

            RetryCount = Cmd.RetryCount;
            Status = CmdStatus.Ready;
        }

        protected abstract void CmdRetry(Result result);

        protected abstract void CmdSent(Result result);

        protected abstract void CmdEnd(Result result);

        private PacketManager PacketHandle;

        private AutoResetEvent SyncWaitFlag = new AutoResetEvent(false);
        public bool IsCmdStop { get; set; } = false;
        public void SyncExcute(PacketManager packetHandle)
        {
            do
            {
                switch (Status)
                {
                    case CmdStatus.Ready:
                        Send(packetHandle);
                        break;
                    case CmdStatus.Requesting:
                        if ((DateTime.Now - RequestedTime).TotalMilliseconds > RequestTimeout)
                        {
                            SendTimeout();
                        }
                        break;
                    case CmdStatus.Requested:
                    case CmdStatus.Responsing:
                        if (ReponseTimeout < 0 ? false : ((DateTime.Now - RequestedTime).TotalMilliseconds > ReponseTimeout))
                        {
                            ReceiveTimeout();
                        }
                        break;
                    case CmdStatus.Responsed:
                        break;
                    case CmdStatus.End:
                        if (IsReapteCmd)
                        {
                            RetryCount = Cmd.RetryCount;
                            Status = CmdStatus.Ready;
                        }
                        return;
                }
                SyncWaitFlag.WaitOne(ThreadPeriod);
            } while (!IsCmdStop);
        }

        public virtual void Send(PacketManager packetHandle)
        {
            PacketHandle = packetHandle;
            Status = CmdStatus.Requesting;
            try
            {
                CmdResult = new Result();
                CmdResult.RequestCmd = this;
                PacketHandle.ReceiveAnalysis = AnalysisPacket;
                PacketHandle.SentEvent = Sent;
                PacketHandle.ReceivedEvent = Received;
                PacketHandle.Ready = true;
                if (GeneratePacket().Length == 0)
                {
                    Status = CmdStatus.Responsing;
                    return;
                }
                PacketHandle.SendPacket(GeneratePacket());
                RequestedTime = DateTime.Now;
            }
            catch
            {
                CmdResult.RespondFailed(Status);
                EndCmd();
            }
        }

        protected virtual void Sent(DateTime requestedTime)
        {
            // TODO: 建立事件
            RequestedTime = requestedTime;
            Status = Status <= CmdStatus.Requested ? CmdStatus.Requested : Status;
            SyncWaitFlag.Set();
            CmdSentEvent?.BeginInvoke(CmdResult, null, null);
            Cmd.CmdSentEvent?.BeginInvoke(CmdResult, null, null);
        }

        public virtual void SendTimeout()
        {
            // TODO: 建立事件
            CmdResult.RespondFailed(Status);
            EndCmd();
        }

        public virtual void ReceiveTimeout()
        {
            if((--RetryCount) > 0)
            {
                Status = CmdStatus.Ready;
                CmdRetryEvent?.BeginInvoke(CmdResult, null, null);
                return;
            }
            CmdResult.RespondFailed(Status);
            EndCmd();
        }

        private void Received(DateTime responsedTime, byte[] ResponsePacket)
        {
            ResponsedTime = responsedTime;
            Status = CmdStatus.Responsed;
            CmdResult.Respond(ResponsePacket);
            if (AnalysisResult(CmdResult))
            {
                CmdResult.CmdSucceed();
            }
            EndCmd();
        }

        protected virtual void EndCmd()
        {
            Status = CmdStatus.End;
            PacketHandle.Ready = false;
            PacketHandle.SentEvent = null;
            PacketHandle.ReceivedEvent = null;
            Cmd.CmdEndEvent?.BeginInvoke(CmdResult, null, null);
            CmdEndEvent?.BeginInvoke(CmdResult, null, null);
        }

        protected abstract bool AnalysisResult(Result result);

        #endregion

        #region Packet

        protected abstract byte[] GeneratePacket();

        protected abstract bool AnalysisPacket(byte ReadByte, ref byte[] ResponseBuffer, ref DateTime ReceivedPacketTime);

        #endregion
    }
}
