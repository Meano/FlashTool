using ISPCore.Util;
using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;

namespace SMTool.Protocol
{
    public interface IProtocolConnector
    {
        SendPacketDelegate SendHandle { get; }

        ReceiveDataDelegate ReceiveHandle { get; set; }
    }

    public delegate bool SendPacketDelegate(byte[] SendPacket);

    public delegate void SentPacketDelegate(DateTime SentTime);

    public delegate void ReceiveDataDelegate(byte[] ReceivedBuffer);

    public delegate bool AnalysisDataDelegate(byte ReceiveByte, ref byte[] AnalysisBuffer, ref DateTime ReceivedPacketTime);

    public delegate void ReceivedPacketDelegate(DateTime ReceiveTime, byte[] ReceivedPacket);

    public class PacketManager
    {
        public SendPacketDelegate SendHandle;

        public SentPacketDelegate SentEvent;

        public PacketManager(IProtocolConnector connector)
        {
            SendHandle = connector.SendHandle;
            connector.ReceiveHandle = ReceiveHandle;
        }
        
        public void SendPacket(byte[] PacketBuffer)
        {
            SendHandle?.BeginInvoke(PacketBuffer, SendCallback, null);
        }

        private void SendCallback(IAsyncResult result)
        {
            var asyncTask = (SendPacketDelegate)((AsyncResult)result).AsyncDelegate;
            asyncTask.EndInvoke(result);
            SentEvent?.Invoke(DateTime.Now);
        }

        public bool Ready = false;

        public ReceivedPacketDelegate ReceivedEvent;

        public AnalysisDataDelegate ReceiveAnalysis;

        public void ReceiveHandle(byte[] ReceiveBuffer)
        {
            if (!Ready) return;
            lock (AnalysisBuffer)
            {
                foreach (byte ReceiveByte in ReceiveBuffer)
                {
                    if (ReceiveAnalysis?.Invoke(ReceiveByte, ref AnalysisBuffer, ref ReceivedPacketTime) == true)
                    {
                        ReceivedEvent?.Invoke(ReceivedPacketTime, (byte[])AnalysisBuffer.Clone());
                        AnalysisBuffer = new byte[0];
                    }
                }
            }
        }

        private byte[] AnalysisBuffer = new byte[0];
        private DateTime ReceivedPacketTime;
    }
}
