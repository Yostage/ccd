using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace CCD2
{
  class TextWindow
  {
    static int boxX, boxY;
    static int boxW, boxH;

    static int cursorPos = 0;

    static string[] strings;
    
    
    /// <summary>
    /// Draw the background for the box.
    /// </summary>
    static void DrawBox()
    {
      string topString, bottomString, lineString;
      
      // Build the border strings
      topString    = "\x00DA".PadRight(boxW-1, '\x00C4') + "\x00BF"; // top-left corner, horizontal lines, top-right corner
      lineString   = "\x00B3".PadRight(boxW-1)           + "\x00B3"; // Vertical line,   empty spaces,     vertical line
      bottomString = "\x00C0".PadRight(boxW-1, '\x00C4') + "\x00D9"; // low-left corner, horizontal lines, low-right corner

      // Write the top
      SuperConsole.WriteColor(boxX, boxY, ConsoleColor.Black, ConsoleColor.DarkGrey, topString);
      
      // Write the necessary middle lines
      for (int i = 1; i < boxH - 1; i++)
        SuperConsole.WriteColor(boxX, boxY + i, ConsoleColor.Black, ConsoleColor.DarkGrey, lineString);
        
      // Finally, the bottom.
      SuperConsole.WriteColor(boxX, boxY + boxH - 1, ConsoleColor.Black, ConsoleColor.DarkGrey, bottomString);
    }


    /// <summary>
    /// Redraw the box with the correct scroll position
    /// </summary>
    static void DrawElements()
    {
      int scrollPos;
      
      // We only want to be at scroll position 0 or max scroll when the cursor is on the first or last element respectively.
      if(cursorPos == 0)
        scrollPos = 0;
      else if(cursorPos == strings.Length-1)
        scrollPos = strings.Length - (boxH - 2);
      else
      {
        int minScroll = -1; // Starting at -1 ensures that it does not start scrolling right away.
        int maxScroll = strings.Length - (boxH - 3); // -3 ensures that it will never scroll all the way down.

        // Get the scroll percentage ( range [0, 1] )
        float fscrollPos = ((float)cursorPos) / ((float)strings.Length - 1);
        
        // Calculate the actual scroll position
        scrollPos = (int)System.Math.Floor(((float)minScroll) + (float)(maxScroll - minScroll) * fscrollPos + 0.3f);
        
        // Ensure it doesn't go below 0.
        if(scrollPos < 0)
          scrollPos = 0;
        
        // Also ensure (corner case) that it doesn't scroll farther than possible
        int trueMax = strings.Length - (boxH - 2);
        if(scrollPos > trueMax)
          scrollPos = trueMax;
      }

      for(int i = 0; i < boxH - 2; i++)
      {
        int element = i + scrollPos;
        
        // Default colors are Black on DarkGrey
        ConsoleColor fg = ConsoleColor.Black;
        ConsoleColor bg = ConsoleColor.DarkGrey;
        
        if (element == cursorPos)
        {
          // This is the selected element, so draw it as white on dark red
          bg = ConsoleColor.DarkRed;
          fg = ConsoleColor.White;
        }
        
        // Build the string
        string s = strings[i + scrollPos];
        
        // Ensure that the string is no longer than the box width (minus 2 for the border), then pad the rest with spaces
        if(s.Length > boxW-2)
          s = s.Substring(0, boxW-2);
        s = s.PadRight(boxW-2);
        
        // Draw!
        SuperConsole.WriteColor(boxX + 1, boxY + i + 1, fg, bg, s);
      }
    }

    
    /// <summary>
    /// Draws a text box containing the specific strings at or near the cursor
    /// </summary>
    /// <param name="strings">The list of strings to display in the text box</param>
    /// <returns>The string that was selected by the Enter key, or null if Escape was pressed</returns>
    public static string Run(string[] strings)
    {
      int len = 0;
      foreach(string s in strings)
        if(s.Length > len)
          len = s.Length;
      return Run(strings, len);
    }
    
    
    
    /// <summary>
    /// Draws a text box containing the specific strings at or near the cursor
    /// </summary>
    /// <param name="strings">The list of strings to display in the text box</param>
    /// <param name="maxLength">The length of the longest string in the list</param>
    /// <returns>The string that was selected by the Enter key, or null if Escape was pressed</returns>
    public static string Run(string[] strings, int maxLength)
    {
      return Run(strings, maxLength, 0);
    }

    /// <summary>
    /// Draws a text box containing the specific strings at or near the cursor
    /// </summary>
    /// <param name="strings">The list of strings to display in the text box</param>
    /// <param name="maxLength">The length of the longest string in the list</param>
    /// <param name="initialIndex">The initial index to select in the list</param>
    /// <returns>The string that was selected by the Enter key, or null if Escape was pressed</returns>
    public static string Run(string[] strings, int maxLength, int initialIndex)
    {
      cursorPos = initialIndex;
      if(cursorPos < 0 || cursorPos >= strings.Length)
        cursorPos = 0;
      
      TextWindow.strings = strings;
      
      // Calculate the box extents (as long as the longest string, but no wider than the console window, similar considerations for height)
      boxW = System.Math.Min(maxLength + 2, Console.WindowWidth);
      boxH = System.Math.Min(Console.WindowHeight, strings.Length + 2);

      // Try to place the box at the cursor.
      boxX = Console.WindowLeft;
      boxY = SuperConsole.CursorY;
      
      // If this position would draw the box off the bottom of the console, move it up until it fits.
      if (boxY + boxH >= Console.WindowHeight)
        boxY = Console.WindowHeight - boxH;
      boxY += Console.WindowTop;
      
      // Cache the background so that we can restore it later.
      SuperConsole.CacheConsole(boxX, boxY, boxW, boxH);

      // Save the current console mode, turn off all mode flags (specifically, no input echo
      SuperConsole.Mode mode = SuperConsole.ConsoleMode;
      SuperConsole.ConsoleMode = SuperConsole.Mode.None;
      
      // Draw the actual box (Background drawing is only done this once, elements drawn as necessary)
      DrawBox();
      DrawElements();
      
      
      bool done = false;
      string retVal = null;
      while (!done)
      {
        switch (SuperConsole.ReadKey())
        {
          case Keys.Escape: // Quit without setting the return value (no retval selected)
            retVal = null;
            done = true;
            break;
            
          case Keys.Right: // Move the cursor down one element
          case Keys.Down:
            if (cursorPos < strings.Length - 1)
            {
              cursorPos++;
              DrawElements();
            }
            break;
            
          case Keys.Left: // Move the cursor up one element
          case Keys.Up:
            if (cursorPos > 0)
            {
              cursorPos--;
              DrawElements();
            }
            break;
            
          case Keys.PageDown: // Move the cursor down one full screen
            cursorPos += boxH - 2;
            if (cursorPos >= strings.Length)
              cursorPos = strings.Length - 1;
            DrawElements();
            break;
            
          case Keys.PageUp: // Move the cursor up one full screen
            cursorPos -= boxH - 2;
            if (cursorPos < 0)
              cursorPos = 0;
            DrawElements();
            break;
            
          case Keys.Home: // Move the cursor to the top
            cursorPos = 0;
            DrawElements();
            break;
            
          case Keys.End: // Move the cursor to the bottom
            cursorPos = strings.Length - 1;
            DrawElements();
            break;
            
          case Keys.Enter: // Select the string currently under the cursor
            retVal = strings[cursorPos];
            done = true;
            break;
        }
      }
      
      // Restore the console mode and the character data that was there before the console was drawn.
      SuperConsole.ConsoleMode = mode;
      SuperConsole.RestoreCache();
      
      // Abandon ship!
      return retVal;
    }
  }
}
