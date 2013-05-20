using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace ccd2
{
  public enum ConsoleColor
  {
    Black        = 0x0,
    DarkBlue     = 0x1,
    DarkGreen    = 0x2,
    DarkCyan     = 0x3,
    DarkRed      = 0x4,
    DarkMagenta  = 0x5,
    DarkYellow   = 0x6,
    Grey         = 0x7,
    DarkGrey     = 0x8,
    LightBlue    = 0x9,
    LightGreen   = 0xA,
    LightCyan    = 0xB,
    LightRed     = 0xC,
    LightMagenta = 0xD,
    LightYellow  = 0xE,
    White        = 0xF
  }

  class SuperConsole
  {
    /// <summary>
    /// Enumeration for virtual keys.
    /// </summary>
    
    #region pinvoke constants
    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_INPUT_HANDLE = -10;
    const uint GENERIC_READ = 0x80000000;
    const uint GENERIC_WRITE = 0x40000000;
    const uint CONSOLE_TEXTMODE_BUFFER = 1;
    const uint INFINITE = 0xFFFFFFFF;
    #endregion
   
    #region pinvoke structures
    [StructLayout(LayoutKind.Explicit)]
    private struct CHAR_INFO
    {
      [FieldOffset(0)]
      internal ushort UnicodeChar;
      [FieldOffset(2)] //2 bytes seems to work properly
      internal UInt16 Attributes;
    }

    //COORD struct
    [StructLayout(LayoutKind.Sequential)]
    private struct COORD
    {
      public short X;
      public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct CONSOLE_SCREEN_BUFFER_INFO
    {

      public COORD dwSize;
      public COORD dwCursorPosition;
      public short wAttributes;
      public SMALL_RECT srWindow;
      public COORD dwMaximumWindowSize;
    }
    
    //SMALL_RECT struct
    [StructLayout(LayoutKind.Sequential)]
    private struct SMALL_RECT
    {
      public short Left;
      public short Top;
      public short Right;
      public short Bottom;
    }

    [FlagsAttribute]
    public enum Mode : uint
    {
      None = 0x0000,
      EnableEchoInput = 0x0004,
      EnableInsertMode = 0x0020,
      EnableLineInput = 0x0002,
      EnableMouseInput = 0x0010,
      EnableProcessedInput = 0x0001,
      EnableQuickEditMode = 0x0040,
      EnableWindowInput = 0x0008,
      EnableProcessedOutput = 0x0001,
      EnableWrapAtEolOutput = 0x0002
    };

    enum EventType : ushort
    {
      Focus = 0x0010,
      Key = 0x0001,
      Menu = 0x0008,
      Mouse = 0x0002,
      WindowBufferSize = 0x0004,
    };
    
    [FlagsAttribute]
    enum ControlKeyState : uint
    {
      None = 0x0000,
      Capslock = 0x0080,
      EnhancedKey = 0x0100,
      LeftAlt = 0x0002,
      LeftCtrl = 0x0008,
      NumLock = 0x0020,
      RightAlt = 0x0001,
      RightCtrl = 0x0004,
      ScrollLock = 0x0040,
      Shift = 0x0010,
    };
    
    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    struct KEY_EVENT_RECORD
    {
      [FieldOffset(0), MarshalAs(UnmanagedType.Bool)]
      public bool bKeyDown;
      [FieldOffset(4), MarshalAs(UnmanagedType.U2)]
      public ushort wRepeatCount;
      [FieldOffset(6), MarshalAs(UnmanagedType.U2)]
      public ushort wVirtualKeyCode;
      [FieldOffset(8), MarshalAs(UnmanagedType.U2)]
      public ushort wVirtualScanCode;
      [FieldOffset(10)]
      public char UnicodeChar;
      [FieldOffset(12), MarshalAs(UnmanagedType.U4)]
      public ControlKeyState dwControlKeyState;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSE_EVENT_RECORD
    {
      public COORD dwMousePosition;
      public uint dwButtonState;
      public uint dwControlKeyState;
      public uint dwEventFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WINDOW_BUFFER_SIZE_RECORD
    {
      public COORD dwSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MENU_EVENT_RECORD
    {
      public uint dwCommandId;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct FOCUS_EVENT_RECORD
    {
      public bool bSetFocus;
    }
        
    [StructLayout(LayoutKind.Explicit)]
    struct INPUT_RECORD
    {
      [FieldOffset(0), MarshalAs(UnmanagedType.U2)]
      public EventType EventType;
      [FieldOffset(4)]
      public KEY_EVENT_RECORD KeyEvent;
      [FieldOffset(4)]
      public MOUSE_EVENT_RECORD MouseEvent;
      [FieldOffset(4)]
      public WINDOW_BUFFER_SIZE_RECORD WindowBufferSizeEvent;
      [FieldOffset(4)]
      public MENU_EVENT_RECORD MenuEvent;
      [FieldOffset(4)]
      public FOCUS_EVENT_RECORD FocusEvent;
    };
    #endregion
   
    #region pinvoke functions
    [DllImport("kernel32.dll")]
    static extern bool SetConsoleCursorPosition(IntPtr hConsoleOutput,
       COORD dwCursorPosition);
    
    [DllImport("kernel32.dll")]
    static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput,
       out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);    
       
    [DllImport("kernel32.dll", EntryPoint = "ReadConsoleInputW", CharSet = CharSet.Unicode)]
    static extern bool ReadConsoleInput(
            IntPtr hConsoleInput,
            [Out] INPUT_RECORD[] lpBuffer,
            uint nLength,
            out uint lpNumberOfEventsRead);      
            
    [DllImport("kernel32.dll")]
    static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    static extern bool ReadConsoleOutput(IntPtr hConsoleOutput, [Out] CHAR_INFO[]
       lpBuffer, COORD dwBufferSize, COORD dwBufferCoord,
       ref SMALL_RECT lpReadRegion);

    [DllImport("kernel32.dll")]
    static extern bool WriteConsoleOutput(IntPtr hConsoleOutput, [In] CHAR_INFO[]
       lpBuffer, COORD dwBufferSize, COORD dwBufferCoord,
       ref SMALL_RECT lpReadRegion);

    [DllImport("kernel32.dll")]
    static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    [DllImport("kernel32.dll")]
    static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern Int32 WaitForSingleObject(IntPtr Handle, UInt32 Wait);

    #endregion
    
    
    /// <summary>
    /// X Coordinate of the console cursor in the buffer.
    /// </summary>
    public static int CursorX
    {
      get
      {
        CONSOLE_SCREEN_BUFFER_INFO info;
        GetConsoleScreenBufferInfo(GetStdHandle(STD_OUTPUT_HANDLE), out info);
        return (int)info.dwCursorPosition.X;
      }
      set
      {
        CONSOLE_SCREEN_BUFFER_INFO info;
        IntPtr handle = GetStdHandle(STD_OUTPUT_HANDLE);
        GetConsoleScreenBufferInfo(handle, out info);
        COORD cursorPos;
        cursorPos.X = (short)value;
        cursorPos.Y = (short)info.dwCursorPosition.Y;
        SetConsoleCursorPosition(handle, cursorPos);
      }
    }

    /// <summary>
    /// Y coordinate of the console cursor in the buffer.
    /// </summary>
    public static int CursorY
    {
      get
      {
        CONSOLE_SCREEN_BUFFER_INFO info;
        GetConsoleScreenBufferInfo(GetStdHandle(STD_OUTPUT_HANDLE), out info);
        return (int)info.dwCursorPosition.Y;
      }
      set
      {
        CONSOLE_SCREEN_BUFFER_INFO info;
        IntPtr handle = GetStdHandle(STD_OUTPUT_HANDLE);
        GetConsoleScreenBufferInfo(handle, out info);
        COORD cursorPos;
        cursorPos.X = (short)info.dwCursorPosition.X;
        cursorPos.Y = (short)value;
        SetConsoleCursorPosition(handle, cursorPos);
      }
    }
        
    /// <summary>
    /// The console flags, which specify what inputs/outputs are enabled.
    /// </summary>
    public static Mode ConsoleMode
    {
      get 
      {
        uint modeTemp;
        GetConsoleMode(GetStdHandle(STD_OUTPUT_HANDLE), out modeTemp);
        return (Mode)modeTemp;
      }
      set
      {
        uint modeTemp = (uint)value;
        SetConsoleMode(GetStdHandle(STD_OUTPUT_HANDLE), modeTemp);
      }
    }
    
    
    /// <summary>
    /// Write a line of text with specific foreground/background colors to the console.
    /// </summary>
    /// <param name="x">The 0-based destination x coordinate.</param>
    /// <param name="y">The 0-based destination y coordinate.</param>
    /// <param name="foreground">Foreground color</param>
    /// <param name="background">Background color</param>
    /// <param name="s">The string to write.</param>
    /// <returns>True if the write was successful, false if there was a failure.</returns>
    public static bool WriteColor(int x, int y, ConsoleColor foreground, ConsoleColor background, string s)
    {
      // the attribute is the combination of the foreground color [0x00, 0x0F] and background color [0x00, 0xF0]
      ushort attribute = (ushort)((int)foreground | (((int)background)<<4));
      
      // Build a CHAR_INFO array containing the characters we want to write and the attributes
      CHAR_INFO[] info = new CHAR_INFO[s.Length];
      char[] array = s.ToCharArray();
      for(int i = 0; i < s.Length; i++)
      {
        info[i].UnicodeChar = (ushort)array[i];
        info[i].Attributes = attribute;
      }
      
      // Writing the length of the string, one character tall.
      COORD myBufferSize;
      myBufferSize.X = (short)s.Length;
      myBufferSize.Y = (short)1;

      // Starting at 0,0 in the source CHAR_INFO array 
      COORD myBufferPosition;
      myBufferPosition.X = 0;
      myBufferPosition.Y = 0;

      // from x..(x+s.Length), one line for y direction.
      SMALL_RECT mySmallRect;
      mySmallRect.Left = (short)x;
      mySmallRect.Right = (short)(x + s.Length);
      mySmallRect.Top = (short)y;
      mySmallRect.Bottom = (short)y;

      // Write.
      return WriteConsoleOutput(GetStdHandle(STD_OUTPUT_HANDLE), info, myBufferSize, myBufferPosition, ref mySmallRect);
    }

    
    /// <summary>
    /// Blocks and waits for a console key to be pressed, then returns the keypress value.
    /// </summary>
    /// <returns>The virtual key code of the key that was pressed</returns>
    public static Keys ReadKey()
    {
      INPUT_RECORD[] buffer = new INPUT_RECORD[1];
      uint events;
      IntPtr handle = GetStdHandle(STD_INPUT_HANDLE);
      for(;;)
      {
        WaitForSingleObject(handle, INFINITE);
        ReadConsoleInput(handle, buffer, 1, out events);
        if(buffer[0].EventType == EventType.Key && buffer[0].KeyEvent.bKeyDown == true)
          return (Keys)buffer[0].KeyEvent.wVirtualKeyCode;
      }
    }

    #region Console Data Cache
    static CHAR_INFO[] cachedCharInfo;
    static int cacheX, cacheY;
    static int cacheW, cacheH;
    static int cacheCursorX, cacheCursorY;
    
    /// <summary>
    /// Caches the character and attribute data of a specified region of the console to be restored later.
    /// </summary>
    /// <param name="x">Left-most coordinate of the region to save</param>
    /// <param name="y">Top-most coordinate of the region to save</param>
    /// <param name="w">Width of the region to save</param>
    /// <param name="h">Height of the region to save</param>
    /// <returns>True if successful, false if failure.</returns>
    public static bool CacheConsole(int x, int y, int w, int h)
    {
      IntPtr hConsoleCache = GetStdHandle(STD_OUTPUT_HANDLE);
      
      // Save the region information so that we know where to restore the character data to
      cacheX = x;
      cacheY = y;
      cacheW = w;
      cacheH = h;
      
      // Save the cursor position for later.
      CONSOLE_SCREEN_BUFFER_INFO info;
      GetConsoleScreenBufferInfo(hConsoleCache, out info);
      cacheCursorX = info.dwCursorPosition.X;
      cacheCursorY = info.dwCursorPosition.Y;

      // We need a CHAR_INFO array that's the correct size.
      cachedCharInfo = new CHAR_INFO[w * h];
      
      // set the width and height
      COORD myBufferSize;
      myBufferSize.X = (short)w;
      myBufferSize.Y = (short)h;

      // start at 0,0 (the first entry) in the source CHAR_INFO array.
      COORD myBufferPosition;
      myBufferPosition.X = 0;
      myBufferPosition.Y = 0;
  
      // The rect that tells the function where to grab the data from
      SMALL_RECT mySmallRect;
      mySmallRect.Left = (short)x;
      mySmallRect.Right = (short)(x + w - 1);
      mySmallRect.Top = (short)y;
      mySmallRect.Bottom = (short)(y + h - 1);
      
      // Read the corresponding data from the screen.
      return ReadConsoleOutput(hConsoleCache, cachedCharInfo, myBufferSize, myBufferPosition, ref mySmallRect);
    }


    /// <summary>
    /// Restore the cached version of the screen to its original state.
    /// </summary>
    public static void RestoreCache()
    {
      int x = cacheX;
      int y = cacheY;
      int w = cacheW;
      int h = cacheH;

      COORD myBufferSize;
      myBufferSize.X = (short)w;
      myBufferSize.Y = (short)h;

      COORD myBufferPosition;
      myBufferPosition.X = 0;
      myBufferPosition.Y = 0;

      SMALL_RECT mySmallRect;
      mySmallRect.Left = (short)x;
      mySmallRect.Right = (short)(x + w);
      mySmallRect.Top = (short)y;
      mySmallRect.Bottom = (short)(y + h);

      IntPtr hConsoleCache = GetStdHandle(STD_OUTPUT_HANDLE);
      
      // Restore the console character/attribute information
      WriteConsoleOutput(hConsoleCache, cachedCharInfo, myBufferSize, myBufferPosition, ref mySmallRect);
      
      // Restore the cursor to its original position
      COORD cursorPos;
      cursorPos.X = (short)cacheCursorX;
      cursorPos.Y = (short)cacheCursorY;
      SetConsoleCursorPosition(hConsoleCache, cursorPos);
    }
    #endregion
  }
}
