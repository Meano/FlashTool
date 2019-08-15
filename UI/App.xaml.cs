using ISPCore.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace SMTool
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            string DllLibraryPath =
                MethodBase.GetCurrentMethod().DeclaringType.Namespace +
                ".Library" +
#if DEBUG
                ".Debug.";
#else
                ".Release.";
#endif
#if !DEBUG
            AddAssemblyResource("ISPCore.dll", DllLibraryPath);
            AddAssemblyResource("Cryptography.dll", DllLibraryPath);
            AppDomain.CurrentDomain.AssemblyResolve += StartupAssembly;
#endif
            Current.DispatcherUnhandledException += UICatchException;
            AppDomain.CurrentDomain.UnhandledException += ThreadCatchException;
        }

        public static string AppDirectoryPath()
        {
            return AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        }

        public static string BuildVersion()
        {
            StringBuilder Version = new StringBuilder();
            Version
                .Append(ResourceAssembly.GetName().Version.Major).Append(".")
                .Append(ResourceAssembly.GetName().Version.Minor).Append(".")
                .Append(ResourceAssembly.GetName().Version.Build);
#if DEBUG
            Version.Append(" (调试版)");
#endif
            return Version.ToString();
        }

        void UICatchException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = "未处理UI线程异常： 异常信息-> " + e.Exception.Message + " 堆栈信息-> " + e.Exception.StackTrace;
            string CrashInfoPath = "./log/CrashInfo-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt"; ;
            FileStream errorFS = new FileStream(CrashInfoPath, FileMode.Create);
            byte[] errorBytes = Encoding.UTF8.GetBytes(errorMessage);
            errorFS.Write(errorBytes, 0, errorBytes.Length);
            errorFS.Flush();
            errorFS.Close();
            Log.error(errorMessage);
            MessageBox.Show(
                "我们很抱歉，当前应用程序遇到一些问题，该操作已经终止，若操作不影响流程，可以重试操作。请保留Log文件。",
                "意外的操作",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            e.Handled = true;
        }

        void ThreadCatchException(object sender, UnhandledExceptionEventArgs e)
        {
            string errorMessage = "未处理线程异常： 异常信息-> " + ((Exception)e.ExceptionObject).Message + " 堆栈信息-> " + ((Exception)e.ExceptionObject).StackTrace;
            string CrashInfoPath = "./log/CrashInfo-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt"; ;
            FileStream errorFS = new FileStream(CrashInfoPath, FileMode.Create);
            byte[] errorBytes = Encoding.UTF8.GetBytes(errorMessage);
            errorFS.Write(errorBytes, 0, errorBytes.Length);
            errorFS.Flush();
            errorFS.Close();
            Log.fatal(errorMessage);
            MessageBox.Show(
                "我们很抱歉，当前应用程序遇到一些问题，程序即将关闭，请保留Log文件。",
                "程序终止",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        private static Dictionary<string, Assembly> DllDictionary = new Dictionary<string, Assembly>();
        private static bool AddAssemblyResource(string FileName, string PrefixName)
        {
            Assembly As = GetAssemblyDll(PrefixName + FileName);
            if (As == null) return false;
            DllDictionary.Add(FileName.Substring(0, FileName.LastIndexOf(".")), As);
            return true;
        }
        private static Assembly GetAssemblyDll(string AssemblyDllPath)
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            using (Stream stream = executingAssembly.GetManifestResourceStream(AssemblyDllPath))
            {
                if (stream == null)
                    return null;
                byte[] assemblyRawBytes = new byte[stream.Length];
                stream.Read(assemblyRawBytes, 0, assemblyRawBytes.Length);
                Assembly As = Assembly.Load(assemblyRawBytes);
                return As;
            }
        }
        private static Assembly StartupAssembly(object sender, ResolveEventArgs args)
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            AssemblyName assemblyName = new AssemblyName(args.Name);
            string ResourceName = assemblyName.Name;
            if (DllDictionary.ContainsKey(ResourceName))
            {
                return DllDictionary[ResourceName];
            }
            else
            {
                return null;
            }
        }
    }
}
