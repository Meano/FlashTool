# Flash Tool
使用C# WPF开发的Flash读写工具Gui及Dell DVAR分析器
## 简介
1. 为了读写主板上的Flash芯片，以修改和更新UEFI BIOS，配合STM32F446的芯片写了PC端的通信程序 [STFlashRW](https://github.com/Meano/STFlashRW) ，使用了我自己写的一个通信协议类模板，快速的生成了一个自定义协议的通信类工具。
2. 分析了Dell的UEFI固件之后，发现在Flash Offset 0x01081000 处的64K数据是Dell自定义的Bios参数数据区，应该是Dell在AMI Bios的基础上做的与AMI Setup Var耦合的参数存储区域，文件头为DVAR，于是通过一些数据经验和猜测解密了DVAR文件的格式，在FlashTool内又写了DVAR文件的分析功能。
3. 已知的一些DVAR Item解析如下：

DVAR Index| DVAR Name          | Desc.
:-: | :-: | :-:
0x00049201| "PPID"             | 主板序列号
0x00042901| "FanCtrlOvrd"      | Smm Fan控制覆盖 置位后风扇不由EC芯片控制 7060表现为全速起飞
0x00042A01| "ChassisPolicy"    | 机箱策略 7060 mt/sff/mff 三款机箱使用同一Bios程序 此处区分机箱类型
0x00261801| "Service Tag"      | Dell服务标签
0x00061701| "Asset Tag"        | 资产标签
0x000E3001| "ProductName"      | Dell产品名称
0x000E3101| "Sku"              | Dell Sku Num. 7060对应085A
0x000E7801| "System Map"       | Bios File Map
0x00250303| "FirstPowerOnDate" | 首次开机时间
0x00250203| "MfgDate"          | 制造时间
0x00062B01| "May Man Mode"     | 推测是工厂模式标志
0x00000102| "May Man Mode1"    | 推测是工厂模式标志
0x00044501| "AcPwrRcvry"       | 电源断电后的回复策略
0x00047801| "WakeOnLan5"       | Wake on Lan配置
