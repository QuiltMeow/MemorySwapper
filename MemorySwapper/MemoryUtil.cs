using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;

namespace MemorySwapper
{
    public class MemoryUtil
    {
        public static void emptyWorkSet()
        {
            Process[] allProcess = Process.GetProcesses();
            foreach (Process process in allProcess)
            {
                try
                {
                    EmptyWorkingSet(process.Handle);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("交換工作區時發生例外狀況 [" + process.ProcessName + "] : " + ex.Message);
                }
            }
        }

        #region P / Invoke 方法

        [DllImport("psapi.dll")]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("ntdll.dll")]
        private static extern uint NtSetSystemInformation(int infoClass, IntPtr info, int length);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, ref long lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivilege, ref TokenPrivilege newState, int bufferLengthInByte, IntPtr previousState, IntPtr returnLengthInByte);

        [DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        #endregion P / Invoke 方法

        #region 資料結構

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SystemCacheInformation
        {
            public uint CurrentSize;
            public uint PeakSize;
            public uint PageFaultCount;
            public uint MinimumWorkingSet;
            public uint MaximumWorkingSet;
            public uint Unused_1;
            public uint Unused_2;
            public uint Unused_3;
            public uint Unused_4;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SystemCacheInformation64Bit
        {
            public long CurrentSize;
            public long PeakSize;
            public long PageFaultCount;
            public long MinimumWorkingSet;
            public long MaximumWorkingSet;
            public long Unused_1;
            public long Unused_2;
            public long Unused_3;
            public long Unused_4;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TokenPrivilege
        {
            public int Count;
            public long Luid;
            public int Attribute;
        }

        public enum SystemInformationClass
        {
            SystemFileCacheInformation = 0x0015,
            SystemMemoryListInformation = 0x0050
        }

        #endregion 資料結構

        private const int PRIVILEGE_ENABLE = 2;
        private const string INCREASE_QUOTA_NAME = "SeIncreaseQuotaPrivilege";
        private const string PROFILE_SINGLE_PROCESS_NAME = "SeProfileSingleProcessPrivilege";
        private const int MEMORY_PURGE_STAND_BY_LIST = 4;

        public static void clearClipboard()
        {
            try
            {
                if (!OpenClipboard(IntPtr.Zero))
                {
                    throw new Exception("OpenClipboard", new Win32Exception(Marshal.GetLastWin32Error()));
                }
                EmptyClipboard();
                CloseClipboard();
            }
            catch (Exception ex)
            {
                MessageBox.Show("清除剪貼簿時發生例外狀況 : " + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public static bool is64Bit()
        {
            return Marshal.SizeOf(typeof(IntPtr)) == 8;
        }

        public static void clearFileSystemCache(bool clearStandByCache = true)
        {
            uint ntSetSystemInformationRet;
            int systemInformationLength;
            GCHandle? gcHandle = null;
            try
            {
                setIncreasePrivilege(INCREASE_QUOTA_NAME);
                try
                {
                    if (!is64Bit())
                    {
                        SystemCacheInformation information = new SystemCacheInformation()
                        {
                            MinimumWorkingSet = uint.MaxValue,
                            MaximumWorkingSet = uint.MaxValue
                        };
                        systemInformationLength = Marshal.SizeOf(information);
                        gcHandle = GCHandle.Alloc(information, GCHandleType.Pinned);
                        ntSetSystemInformationRet = NtSetSystemInformation((int)SystemInformationClass.SystemFileCacheInformation, gcHandle.Value.AddrOfPinnedObject(), systemInformationLength);
                    }
                    else
                    {
                        SystemCacheInformation64Bit information64Bit = new SystemCacheInformation64Bit()
                        {
                            MinimumWorkingSet = -1,
                            MaximumWorkingSet = -1
                        };
                        systemInformationLength = Marshal.SizeOf(information64Bit);
                        gcHandle = GCHandle.Alloc(information64Bit, GCHandleType.Pinned);
                        ntSetSystemInformationRet = NtSetSystemInformation((int)SystemInformationClass.SystemFileCacheInformation, gcHandle.Value.AddrOfPinnedObject(), systemInformationLength);
                    }
                }
                finally
                {
                    if (gcHandle != null)
                    {
                        gcHandle.Value.Free();
                        gcHandle = null;
                    }
                }
                if (ntSetSystemInformationRet != 0)
                {
                    throw new Exception("NtSetSystemInformation", new Win32Exception(Marshal.GetLastWin32Error()));
                }

                if (!clearStandByCache)
                {
                    return;
                }
                setIncreasePrivilege(PROFILE_SINGLE_PROCESS_NAME);
                systemInformationLength = Marshal.SizeOf(MEMORY_PURGE_STAND_BY_LIST);
                try
                {
                    gcHandle = GCHandle.Alloc(MEMORY_PURGE_STAND_BY_LIST, GCHandleType.Pinned);
                    ntSetSystemInformationRet = NtSetSystemInformation((int)SystemInformationClass.SystemMemoryListInformation, gcHandle.Value.AddrOfPinnedObject(), systemInformationLength);
                }
                finally
                {
                    if (gcHandle != null)
                    {
                        gcHandle.Value.Free();
                        gcHandle = null;
                    }
                }
                if (ntSetSystemInformationRet != 0)
                {
                    throw new Exception("NtSetSystemInformation (記憶體列表)", new Win32Exception(Marshal.GetLastWin32Error()));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("清除檔案系統緩存時發生例外狀況 : " + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void setIncreasePrivilege(string privilegeName)
        {
            using (WindowsIdentity current = WindowsIdentity.GetCurrent(TokenAccessLevels.Query | TokenAccessLevels.AdjustPrivileges))
            {
                TokenPrivilege tokenPrivilege = new TokenPrivilege()
                {
                    Attribute = PRIVILEGE_ENABLE,
                    Count = 1,
                    Luid = 0
                };
                if (!LookupPrivilegeValue(null, privilegeName, ref tokenPrivilege.Luid))
                {
                    throw new Exception("LookupPrivilegeValue", new Win32Exception(Marshal.GetLastWin32Error()));
                }
                bool adjustTokenPrivilegeRet = AdjustTokenPrivileges(current.Token, false, ref tokenPrivilege, 0, IntPtr.Zero, IntPtr.Zero);
                if (!adjustTokenPrivilegeRet)
                {
                    throw new Exception("AdjustTokenPrivileges", new Win32Exception(Marshal.GetLastWin32Error()));
                }
            }
        }

        private static readonly ComputerInfo COMPUTER_INFO = new ComputerInfo();

        private static double getRAMPercent()
        {
            double total = Convert.ToDouble(COMPUTER_INFO.TotalPhysicalMemory);
            double use = total - Convert.ToDouble(COMPUTER_INFO.AvailablePhysicalMemory);
            double percent = use / total * 100;

            if (double.IsNaN(total) || double.IsInfinity(total))
            {
                throw new ArgumentException(nameof(total));
            }
            if (double.IsNaN(use) || double.IsInfinity(use))
            {
                throw new ArgumentException(nameof(use));
            }
            if (double.IsNaN(percent) || double.IsInfinity(percent))
            {
                throw new ArgumentException(nameof(percent));
            }
            return percent;
        }

        public static void fillRAM(int maxRun = 1)
        {
            int run = 0;
            while (run < maxRun)
            {
                List<IntPtr> pointerList = new List<IntPtr>();
                try
                {
                    for (double percent = getRAMPercent(); percent < 99; percent = getRAMPercent())
                    {
                        IntPtr pointer = Marshal.AllocHGlobal(1024); // 1 KB
                        pointerList.Add(pointer);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("填滿記憶體空間時發生例外狀況 : " + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                foreach (IntPtr clearPointer in pointerList)
                {
                    try
                    {
                        Marshal.FreeHGlobal(clearPointer);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("清除記憶體空間時發生例外狀況 : " + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                ++run;
            }
        }
    }
}