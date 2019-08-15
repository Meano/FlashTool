using ISPCore.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;

namespace SMTool.Protocol
{
    public class CmdManager
    {
        public CmdManager(IProtocolConnector connector)
        {
            PacketHandle = new PacketManager(connector);
        }

        public PacketManager PacketHandle;

        private static readonly object CmdLock = new object();

        public int CmdCount
        {
            get
            {
                return CmdQueue.Count;
            }
        }

        public bool Add(ICmd cmd)
        {
            lock (CmdLock)
            {
                CmdQueue.Enqueue(cmd);
                return true;
            }
        }

        public bool AddRange(ICmd[] cmds)
        {
            lock (CmdLock)
            {
                if (CmdQueue.Count != 0) return false;
                foreach (ICmd cmd in cmds)
                {
                    CmdQueue.Enqueue(cmd);
                }
                CmdQueue.Peek().IsReapteCmd = true;
                return true;
            }
        }

        public void Clear()
        {
            lock (CmdLock)
            {
                ICmd cmdhandle = CurrentCmd;
                CmdQueue.Clear();
                if(cmdhandle != null)
                {
                    cmdhandle.IsCmdStop = true;
                }
            }
        }

        public bool IsBusy
        {
            get
            {
                return CmdResult != null && !CmdResult.IsCompleted;
            }
        }

        public bool Start()
        {
            if (IsBusy)
            {
                Log.warn("命令队列未完成，请稍后重试！");
                return false;
            }
            CmdCostTime = 0;
            CmdAction = CmdTask;
            CmdStartTime = DateTime.Now;
            CmdResult = CmdAction.BeginInvoke(CmdComplete, CmdAction);
            return true;
        }

        private ICmd CurrentCmd
        {
            get
            {
                if (CmdCount > 0)
                    return CmdQueue.Peek();
                else
                    return null;
            }
        }

        private bool Next()
        {
            ICmd CmdHandle = CurrentCmd;
            if (CmdHandle == null) return false;
            lock (CmdHandle)
            {
                if (CmdHandle.IsReapteCmd)
                {
                    return true;
                }
                else if (CmdQueue.Count > 1 && CmdHandle.IsSuccess)
                {
                    CmdQueue.Dequeue();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }


        private Queue<ICmd> CmdQueue = new Queue<ICmd>();
        private Action CmdAction;
        private IAsyncResult CmdResult;

        private DateTime CmdStartTime { get; set; }
        private double CmdCostTime { get; set; }
        private double TaskCostTime { get; set; }
        private AutoResetEvent CmdEvent = new AutoResetEvent(false);

        public bool IsPrivateCmd = false;

        private void CmdTask()
        {
            while (true)
            {
                ICmd cmdHandle = CurrentCmd;
                if (cmdHandle == null) return;
                cmdHandle.SyncExcute(PacketHandle);
                IsPrivateCmd = false;
                double ResponsedTime = 0;
                if (cmdHandle.IsSuccess)
                {
                    ResponsedTime = (cmdHandle.ResponsedTime - cmdHandle.RequestedTime).TotalMilliseconds;
                }
                CmdCostTime += ResponsedTime;
                if (!IsPrivateCmd) Log.info("本次命令执行耗时" + ResponsedTime.ToString() + "ms!");
                if (Next())
                {
                    if (!IsPrivateCmd) Log.info("将执行下一条命令!");
                    CmdEvent.Set();
                }
                else
                {
                    return;
                }
            }
        }

        private void CmdComplete(IAsyncResult result)
        {
            var asyncTask = (Action)((AsyncResult)result).AsyncDelegate;
            asyncTask.EndInvoke(result);
            TaskCostTime = (DateTime.Now - CmdStartTime).TotalMilliseconds;
            if (!IsPrivateCmd) Log.info("===============================");
            if (!IsPrivateCmd) Log.info("命令耗时" + CmdCostTime.ToString("F3") + "ms ( " + (CmdCostTime * 0.001).ToString("F3") + "s )。");
            if (!IsPrivateCmd) Log.info("任务耗时" + TaskCostTime.ToString("F3") + "ms ( " + (TaskCostTime * 0.001).ToString("F3") + "s )。");
            Clear();
        }
    }
}
