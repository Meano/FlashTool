using ISPCore.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SMTool.Protocol.FlashCmd
{
    public class FlashCmdTemplate : CmdTemplate
    {
        public delegate byte[] PacketGeneratorDelegate(params string[] contents);

        public delegate bool PacketAnalysisDelegate(FlashCmdResult result);

        public byte SendCmd;

        public PacketAnalysisDelegate PacketAnalysis;

        public FlashCmdTemplate(string name, byte cmd, PacketAnalysisDelegate packetAnalysis) : base(name)
        {
            SendCmd = cmd;
            PacketAnalysis = packetAnalysis;
        }
    }

    public class FlashCmdResult : CmdResult
    {
        public byte SendCmd;
        public byte ResponseCmd;
        public uint ResponsePara;
        public byte[] Payload;
    }

    public class FlashCmd : Cmdbase<FlashCmdTemplate, FlashCmdResult>
    {

        public uint CmdPara;
        public byte[] CmdContent;

        public FlashCmd(string cmdName, uint para, byte[] content) : base(cmdName)
        {
            CmdPara = para;
            CmdContent = content != null ? content : new byte[0];
            ReadByteList = new List<byte>();
            CmdResult.SendCmd = Cmd.SendCmd;
        }

        private List<byte> ReadByteList;
        ushort PacketLength;
        byte Sum;
        protected override bool AnalysisPacket(byte ReadByte, ref byte[] ResponseBuffer, ref DateTime ReceivedPacketTime)
        {
            if (ReadByteList.Count() == 0 && ReadByte == 0xAA)
            {
                ReceivedPacketTime = DateTime.Now;
                PacketLength = 0;
                Sum = 0;
            }
            else if (ReadByteList.Count() < 7)
            {

            }
            else if(ReadByteList.Count() == 7)
            {
                PacketLength = (ushort)(ReadByteList[6] << 8 | ReadByte);
                if (PacketLength > 4096) {
                    ReadByteList.Clear();
                    return false;
                } 
            }
            else if(ReadByteList.Count() < (PacketLength + 8))
            {
                Sum += ReadByte;
            }
            else if(ReadByteList.Count() == (PacketLength + 8))
            {
                if(ReadByte != (byte)(~Sum))
                {
                    Log.warn("Sum error");
                    ReadByteList.Clear();
                    return false;
                }
                ResponseBuffer = ReadByteList.ToArray();
                ReadByteList.Clear();
                return true;
            }
            else
            {
                return false;
            }
            ReadByteList.Add(ReadByte);
            return false;
        }

        protected override bool AnalysisResult(FlashCmdResult result)
        {
            if(result.Packet.Length < 8)
            {
                result.CmdFailed("CmdPacketLengthError");
                return false;
            }
            result.ResponseCmd = result.Packet[1];
            result.ResponsePara = (uint)(
                (result.Packet[2] << 24) |
                (result.Packet[3] << 16) |
                (result.Packet[4] <<  8) |
                (result.Packet[5] <<  0));
            result.Payload = result.Packet.Skip(8).ToArray();
            
            if(result.ResponseCmd == (Cmd.SendCmd | 0x80) && result.ResponsePara == CmdPara)
            {
                return true;
            }
            else
            {
                Log.warn("Cmd: " + Cmd.SendCmd.ToString("X02") + ":" + result.ResponseCmd.ToString("X02") +
                    " Para: " + result.ResponsePara.ToString("X08") + ":" + CmdPara.ToString("X08"));
                result.CmdFailed("CmdError");
                return false;
            }
        }

        protected override void CmdEnd(FlashCmdResult result)
        {
            FlashCmd cmd = (FlashCmd)result.RequestCmd;
            Cmd.PacketAnalysis?.BeginInvoke(result, null, null);
            if (result.Packet != null)
                Log.debug("PRecv-> " + result.ResponseCmd.ToString("X02") + " " + result.ResponsePara.ToString("X08") + " " + Tool.HexToString(result.Payload));
            Log.info("Cmd" + cmd.CmdName + (result.IsSuccess ? "执行成功": ("执行失败" + result.FailMessage)));
        }

        protected override void CmdRetry(FlashCmdResult result)
        {
            ReadByteList.Clear();
        }

        protected override void CmdSent(FlashCmdResult result)
        {
            ;
        }

        protected override byte[] GeneratePacket()
        {
            List<byte> packetlist = new List<byte>();
            packetlist.Add(0xAA);
            packetlist.Add(Cmd.SendCmd);
            packetlist.Add((byte)((CmdPara & 0xFF000000) >> 24));
            packetlist.Add((byte)((CmdPara & 0x00FF0000) >> 16));
            packetlist.Add((byte)((CmdPara & 0x0000FF00) >>  8));
            packetlist.Add((byte)((CmdPara & 0x000000FF) >>  0));
            ushort contentlen = (ushort)CmdContent.Length;
            packetlist.Add((byte)(contentlen >> 8));
            packetlist.Add((byte)(contentlen & 0xff));
            packetlist.AddRange(CmdContent);
            byte sum = 0;
            foreach (byte contentbyte in CmdContent)
            {
                sum += contentbyte;
            }
            packetlist.Add((byte)(~sum));
            return packetlist.ToArray();
        }
    }
}
