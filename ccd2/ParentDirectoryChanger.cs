using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace CCD2
{
  class ParentDirectoryChanger
  {
    #region pinvoke constants
    const Int64 INVALID_HANDLE_VALUE = -1;
    const int MEM_COMMIT = 0x1000;
    const int PAGE_READWRITE = 0x04;
    const int MEM_RELEASE = 0x8000;
    const UInt32 INFINITE = 0xFFFFFFFF;
    #endregion
    
    #region pinvoke structures
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Auto)]
    private struct PROCESSENTRY32
    {
      const int MAX_PATH = 260;
      internal UInt32 dwSize;
      internal UInt32 cntUsage;
      internal UInt32 th32ProcessID;
      internal IntPtr th32DefaultHeapID;
      internal UInt32 th32ModuleID;
      internal UInt32 cntThreads;
      internal UInt32 th32ParentProcessID;
      internal Int32 pcPriClassBase;
      internal UInt32 dwFlags;
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst=MAX_PATH)]
      internal string szExeFile;
    }

    [Flags]
    public enum SnapshotFlags : uint
    {
      HeapList=0x00000001,
      Process=0x00000002,
      Thread=0x00000004,
      Module=0x00000008,
      Module32=0x00000010,
      Inherit=0x80000000,
      All=0x0000001F
    }

    [Flags]
    enum ProcessFlags : uint
    {
      CreateThread=0x02,
      VMOperation=0x08,
      VMRead=0x10,
      VMWrite=0x20
    }
    #endregion
    
    #region pinvoke functions
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags,
       uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress,
       UIntPtr dwSize, uint dwFreeType);

    [DllImport("kernel32.dll", SetLastError=true)]
    static extern IntPtr CreateToolhelp32Snapshot(SnapshotFlags dwFlags, uint th32ProcessID);

    [DllImport("kernel32", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    static extern bool Process32First([In]IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    static extern bool Process32Next([In]IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(UInt32 dwDesiredAccess, Int32 bInheritHandle, UInt32 dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hHandle);

    [DllImport("kernel32.dll", SetLastError=true, ExactSpelling=true)]
    static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress,
                  uint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern Int32 WaitForSingleObject(IntPtr Handle, UInt32 Wait);
    
    [DllImport("kernel32.dll")]
    static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
      byte [] lpBuffer, UIntPtr nSize, out IntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    
    [DllImport("kernel32.dll")]
    static extern IntPtr CreateRemoteThread(IntPtr hProcess,
       IntPtr lpThreadAttributes, uint dwStackSize, IntPtr
       lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr LoadLibrary(string lpszLib);
    #endregion
    
    
    /// <summary>
    /// Get the Process ID of the parent process of this one (which should be the console window)
    /// </summary>
    /// <returns></returns>
    static int GetParentProcId()
    {
      // First we need the current process id.
      int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
      
      // Now get a snapshot of the currently-running processes
      PROCESSENTRY32 procEntry = new PROCESSENTRY32();
      procEntry.dwSize = (UInt32)Marshal.SizeOf(typeof(PROCESSENTRY32));
      IntPtr snap = CreateToolhelp32Snapshot(SnapshotFlags.Process, 0);
      
      // Loop through each process (if the initial call succeeds)      
      if(Process32First(snap, ref procEntry))
      {
        do
        {
          // Search for our specific process
          if(pid == procEntry.th32ProcessID)
          {
            // If we find it, return our parent process id
            return (int)procEntry.th32ParentProcessID;
          }
        }
        while(Process32Next(snap, ref procEntry));
      }
      throw new ApplicationException("Can't get parent process id.");
    }
    
    
    /// <summary>
    /// Change the working directory for a given process
    /// </summary>
    /// <param name="proc">Handle (as IntPtr from marshalling) to the process to update</param>
    /// <param name="dir">The directory to apply as the cwd of the target process</param>
    static void SetProcessDir(IntPtr proc, string dir)
    {
      // Copy the unicode version of the string to a byte array.
      System.Text.UnicodeEncoding  encoding=new System.Text.UnicodeEncoding();
      Byte[] bytes = encoding.GetBytes(dir);      
      
      // Allocate enough memory in the target process to copy the entire string over.
      IntPtr ptr = VirtualAllocEx(proc, (IntPtr)0, (uint)bytes.Length, MEM_COMMIT, PAGE_READWRITE);
      
      
      if(ptr != null)
      {
        // Copy the string to the target process.
        IntPtr bytesWritten;
        WriteProcessMemory(proc, ptr, bytes, (UIntPtr)bytes.Length, out bytesWritten);
        
        // Get a pointer to SetCurrentDirectoryW from kernel32.dll
        IntPtr kernel32 = LoadLibrary("kernel32.dll");
        IntPtr setCurrentDirectory = GetProcAddress(kernel32, "SetCurrentDirectoryW");
        
        // Call SetCurrentDirectoryW in a new thread in the target process, and pass it the pointer to the buffer that was allocated
        // in its process.
        
        // This takes advantage of the fact that SetCurrentDirectoryW has the exact same C function signature as a thread process
        // and, since it's a kernel function, it exists in the target process.
        IntPtr threadHandle = CreateRemoteThread(proc, (IntPtr)0, 0, setCurrentDirectory, ptr, 0, (IntPtr)0);
        
        if(threadHandle != (IntPtr)0)
        {
          // Wait for it to finish.  The directory is now changed.
          WaitForSingleObject(threadHandle, INFINITE);
          CloseHandle(threadHandle);
        }
        
        // Free our memory, that's all we needed.
        VirtualFreeEx(proc, ptr, (UIntPtr)0, MEM_RELEASE);
      }
    }
    
    
    /// <summary>
    /// Change the parent process' current working directory.
    /// </summary>
    /// <param name="dir">The target directory for the parent process.</param>
    public static void Change(string dir)
    {
      // Get a handle (IntPtr) to the parent process.
      int parentID = GetParentProcId();
      
      IntPtr parentProcess = OpenProcess((uint)(ProcessFlags.VMOperation|ProcessFlags.CreateThread|ProcessFlags.VMRead|ProcessFlags.VMWrite), 0, (uint)parentID);
      if(parentProcess != (IntPtr)0)
      { 
        // Apply the directory to that process.
        SetProcessDir(parentProcess, dir);
        CloseHandle(parentProcess);
      }
    }
  }
}
