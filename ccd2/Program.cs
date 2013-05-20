using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace ccd2
{
  static class Program
  {
    #region Cross-process mutexes
    static System.Threading.Mutex rootMutex   = new System.Threading.Mutex(false, "CCDRootMutex");
    static System.Threading.Mutex listMutex   = new System.Threading.Mutex(false, "CCDListMutex");
    static System.Threading.Mutex scanMutex   = new System.Threading.Mutex(false, "CCDScanMutex");
    static System.Threading.Mutex ignoreMutex = new System.Threading.Mutex(false, "CCDIgnoreMutex");
    #endregion
    
    
    /// <summary>
    /// Checks to see if there is background scanning by attempting to acquire the scan mutex.
    /// </summary>
    /// <returns>True if there is background scanning, false otherwise</returns>
    static bool IsBackgroundScanning()
    {
      if(scanMutex.WaitOne(0,false))
      {
        scanMutex.ReleaseMutex();
        return false;
      }
      return true;
    }
    
    
    
    /// <summary>
    /// If it doesn't already have one, add a backslash to the end of the given string
    /// </summary>
    /// <param name="dir">the directory string to backslashify</param>
    /// <returns>A new string with the backslash added</returns>
    static string TrailBackslash(string dir)
    {
      if(!dir.EndsWith("\\"))
        dir += "\\";
      return dir;
    }
    
    
    /// <summary>
    /// Create the directory if it doesn't already exist
    /// </summary>
    /// <param name="dir">The directory to potentially create</param>
    static void EnsureDirectoryExists(string dir)
    {
      if(!Directory.Exists(dir))
      {
        Directory.CreateDirectory(dir);
      }
    }


    /// <summary>
    /// Remove any paths from the given list that belong to the supplied root.
    /// </summary>
    /// <param name="root"></param>
    /// <param name="paths"></param>
    static void RemoveRootPaths(string root, List<string> paths)
    {
      RemoveRootPaths(new string[] { root }, paths);
    }


    /// <summary>
    /// Remove any paths from the given list that belong to any of the supplied roots.
    /// </summary>
    /// <param name="roots">the roots that are to be removed.</param>
    /// <param name="paths">A list of paths from which elements will be removed</param>
    static void RemoveRootPaths(string[] roots, List<string> paths)
    {
      // Remove from the path list any paths that belong to any of the given roots.
      for(int i = 0; i < paths.Count; )
      {
        bool found = false;
        foreach(string root in roots)
        {
          if(paths[i].StartsWith(root))
          {
            paths.RemoveAt(i);
            found = true;
            break;
          }
        }
        if(!found)
          i++;
      }
    }
    
    
    /// <summary>
    /// Build a regular expression around a set of args based on the first part of the regex, what goes between each argument,
    /// and the end of the regex string.
    /// </summary>
    /// <param name="args">The arguments for the regex.  Each string in this will be escaped before being inserted into the regex</param>
    /// <param name="first">The string that will start the regex (the beginning of the regex)</param>
    /// <param name="middle">The string that will be placed between each argument in the regex</param>
    /// <param name="end">The string that will end the regex</param>
    /// <returns>The Regex that is built by this function.</returns>
    static Regex BuildSearchRegex(string[] args, string first, string middle, string end)
    {
      bool initial = true;
      StringBuilder builder = new StringBuilder(first);
      foreach(string arg in args)
      {
        if (!initial)
          builder.Append(middle);
        initial = false;
        
        builder.Append(Regex.Escape(arg));
      }
      builder.Append(end);
      return new Regex(builder.ToString());
    }
    
    
    /// <summary>
    /// Tests each string in a list against the supplied regex, any matches are added to the matches list.
    /// </summary>
    /// <param name="regex">The regular expression to compare against</param>
    /// <param name="cachedStrings">A list of strings to test against the regex</param>
    /// <param name="matches">A list that gets added to when matches are found</param>
    /// <returns>The length of the longest string in the match list.</returns>
    static int RunTest(Regex regex, string[] cachedStrings, List<string> matches)
    {
      int maxLength = 0;
      foreach (string line in cachedStrings)
      {
        if(regex.IsMatch(line))
        {
          if(line.Length > maxLength)
            maxLength = line.Length;
          matches.Add(line);
        }
      }
      return maxLength;
    }      
  
    /// <summary>
    /// Displays the help text.
    /// </summary>
    static void DisplayHelp()
    {
      string[] helpText = new string[]
      {
        "ccd3 (Josh Jersild)",
        "Usage:",
        "  ccd [-a|-searchall] <tokens>",
        "      -or-",
        "  ccd <option> [option parameters]",
        "",
        " Options:",
        "   -a or -searchall",
        "      Scan from all indexed root locations instead of just the",
        "      root corresponding to the current directory.",
        "",
        "   -addroot <directory>",
        "      Add a directory that will be indexed by ccd",
        "",
        "   -delroot <directory>",
        "      Remove an indexed location from the list", 
        "",
        "   -listroots",
        "      Display all of the indexed root directories in the list",
        "",
        "   -addignore <directory> [tokens]",
        "      Add an ignore token set to the ignore list.  For instance:",
        "      ccd -addignore c:\\source obj",
        "      would ignore all directories named 'obj' under the c:\\source directory.",
        "",
        "   -delignore <directory> [tokens]",
        "      Remove an ignore token set from the ignore list.",
        "",
        "   -listignores",
        "      List all of the ignore tokens in the list",
        "",
        "   -rescan [all]",
        "      Rescan the root location corresponding to the current directory.",
        "      If the \"all\" keyword is present, it will rescan ALL indexed locations.",
        "      This will likely be slow.",
      };      
      TextWindow.Run(helpText);
    } 
    
    
    /// <summary>
    /// Adds a new search root to the system
    /// </summary>
    /// <param name="root"></param>
    static void AddRoot(string root)
    { 
      // Lowercase and add the backslash to the root
      root = TrailBackslash(root.ToLowerInvariant());
      
      // The root directory must exist to be added to the list.
      if(!Directory.Exists(root))
      {
        Console.WriteLine("Root path \"" + root + "\" does not exist");
        return;
      }

      bool alreadyInList = false;
      rootMutex.WaitOne();
      {
        // Create the app dir if it doesn't exist.
        EnsureDirectoryExists(AppDir);
        
        // Load the root data
        string[] roots = LoadRoots();
        
        // Scan the root data to see if the root is already in the list.
        foreach(string existingRoot in roots)
        {
          if(existingRoot == root)
          {
            alreadyInList = true;
            Console.WriteLine("Root \"" + root + "\" is already in the list");
            break;
          }
        }
        
        // If it's not already in the list
        if(!alreadyInList)
        {
          // Copy the root array to the end of a new array
          string[] newRoots = new string[roots.Length+1];
          Array.Copy(roots, 0, newRoots, 1, roots.Length);
          
          // Add the new root to the beginning of the list.
          newRoots[0] = root;
          Array.Sort<string>(newRoots);
          
          // Write them to file
          WriteRoots(newRoots);
        }
      }
      rootMutex.ReleaseMutex();
      
      if(!alreadyInList)
      {
        // If we added a new root, scan the root now.
        Rescan(root);
      }
    }
    
    
    /// <summary>
    /// Deletes a search root from the system
    /// </summary>
    /// <param name="root"></param>
    static void DelRoot(string root)
    {
      // Lowercase, backslash added
      root = TrailBackslash(root.ToLowerInvariant());
      bool removed = false;

      rootMutex.WaitOne();
      {
        // Create a list from the roots file
        List<string> rootList = new List<string>(LoadRoots());
        
        // If we find one to remove, remove it from the list.
        for(int i = 0; i < rootList.Count; i++)
        {
          if(rootList[i] == root)
          {
            rootList.RemoveAt(i);
            removed = true;
            break;
          }
        }
        
        if(removed)
        { 
          // Write the new roots file if modifications were made.
          WriteRoots(rootList.ToArray());
        }
        else
        {
          // Tell the user nothing happened
          Console.WriteLine("Root \"" + root + "\" is not in the list.");
        }
      }
      rootMutex.ReleaseMutex();
      
      if(removed)
      {
        listMutex.WaitOne();
        {
          // Load the path data, remove all paths belonging to the removed root, and write the path data back to the file.
          List<string> pathList = new List<string>(LoadPaths());
          RemoveRootPaths(root, pathList);
          WritePaths(pathList.ToArray());
        }
        listMutex.ReleaseMutex();
      }
    }
    
    
    
    /// <summary>
    /// Displays a list of all of the roots.
    /// </summary>
    static void ListRoots()
    {
      // Load root data (we don't need to hold the mutex here, the LoadRoots function does it
      string[] roots = LoadRoots();
      if(roots.Length == 0)
      {
        Console.WriteLine("No roots to display.");
        return;
      }
      
      // Display a nice text window containing all of the roots
      TextWindow.Run(roots);
    }
    
    
    /// <summary>
    /// Spawn a new process to scan the directory structure in the background.
    /// </summary>
    /// <param name="root">The root directory to scan</param>
    static void RescanBackground(string root)
    {
      if(IsBackgroundScanning())
      {
        Console.WriteLine("Current directory not found in list.  Not triggering a rescan");
        Console.WriteLine("because another process is already scanning.");
        return;
      }
      Console.WriteLine("Current directory not found in list.  Triggering a background rescan");
      System.Diagnostics.Process p = new System.Diagnostics.Process();
      p.StartInfo.Arguments = "-rescan";
      p.StartInfo.WorkingDirectory = root;
      p.StartInfo.FileName = "ccd.exe";
      p.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
      p.Start();
    }
        
    /// <summary>
    /// Rescan a given root's directory structure
    /// </summary>
    /// <param name="root"></param>
    static void Rescan(string root)
    {
      Rescan(new string[] { root });
    }
    
    
    /// <summary>
    /// Rescan the current directory's root
    /// </summary>
    static void RescanCurrentRoot()
    {

      // Find the current directory (backslash trailed and lowercase)
      string curDir = TrailBackslash(System.Environment.CurrentDirectory.ToLowerInvariant());

      // Scour the root list for the current directory
      string[] roots = LoadRoots();
      foreach(string root in roots)
      {
        if(curDir.StartsWith(root))
        {
          // We found the current directory root, so rescan it and return.
          Rescan(root);
          return;
        }
      }
      
      Console.WriteLine("Nothing to rescan.  Current directory does not belong to any specific root");
    }


    /// <summary>
    /// Rescan all roots.
    /// </summary>
    static void RescanAll()
    {
      // Simply load all of the roots from file and rescan them.
      Rescan(LoadRoots());
    }


    /// <summary>
    /// Rescan all of the supplied roots.
    /// </summary>
    /// <param name="roots"></param>
    static void Rescan(string[] roots)
    {
      scanMutex.WaitOne();
      {
        // Load a list of the paths
        List<string> pathList = new List<string>(LoadPaths());
        
        // Remove from the path list any paths that belong to any of the given roots.
        RemoveRootPaths(roots, pathList);
        
        // We'll maintain a stack of the paths that need to be searched.  Start by pushing all of the roots onto it.
        Stack<string> pathsToSearch = new Stack<string>();
        for(int i = roots.Length-1; i >= 0; i--)
          pathsToSearch.Push(roots[i]);

        // Finally, we need the regular expressions corresponding to the ignore rules.
        Regex[] ignores = LoadIgnoreRegexes(roots);
        
        System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        watch.Start();
        
        const float timeDiff = 0.5f;
        float prevTime  = ((float)watch.ElapsedMilliseconds)/1000 - timeDiff - 0.1f;
        
        Console.WriteLine();
        int writeX = Console.WindowLeft;
        int writeY = SuperConsole.CursorY-1;
        SuperConsole.CacheConsole(writeX, writeY, Console.WindowWidth, 2);
        SuperConsole.WriteColor(writeX, writeY, ConsoleColor.LightYellow, ConsoleColor.DarkBlue, "Rescanning...".PadRight(Console.WindowWidth));
        
        // For each directory
        while(pathsToSearch.Count != 0)
        {
          // Make sure to add backslashes as necessary.
          string path = TrailBackslash(pathsToSearch.Pop());
          
          // If this path matches any of the ignore lists, we'll skip it completely.
          bool ignore = false;
          for(int i = 0; i < ignores.Length; i++)
          {
            if(ignores[i].IsMatch(path))
            {
              ignore = true;
              break;
            }
          }
          if(ignore)
            continue;

          // If enough time has elapsed, display a new directory
          float curTime = ((float)watch.ElapsedMilliseconds)/1000;
          if(curTime - prevTime > timeDiff)
          {
            prevTime += timeDiff;
            SuperConsole.WriteColor(writeX, writeY+1, ConsoleColor.White, ConsoleColor.DarkBlue, path.PadRight(Console.WindowWidth));
            System.Threading.Thread.Sleep(1);
          }

          // Get the subdirectories (wrapped in a try block mostly to avoid the access denied errors that some folders give)
          string[] newDirs;
          try
          {
            newDirs = Directory.GetDirectories(path);
          }
          catch(Exception) { newDirs = new string[0]; }

          // Add the lower-cased current path to the list.
          pathList.Add(path.ToLowerInvariant());
          
          // Add each subdirectory to the scan list.
          for(int i = newDirs.Length-1; i >= 0; i--)
            pathsToSearch.Push(newDirs[i]);
        }
        SuperConsole.RestoreCache();
        SuperConsole.CursorY--;
        
        // Sort and write the path list.
        pathList.Sort();
        WritePaths(pathList.ToArray());
      }
      scanMutex.ReleaseMutex();
    }
    
    
    /// <summary>
    /// Build a list of regexes from the supplied ignore strings.
    /// </summary>
    /// <param name="strings"></param>
    /// <returns></returns>
    static Regex[] BuildIgnoreRegexes(string[] strings)
    {
      Regex[] regexes = new Regex[strings.Length];
      for (int i = 0; i < strings.Length; i++)
      {
        // Split the function by spaces
        string[] div = strings[i].Split(' ');
        
        // Copy all but the first string in the list to a token array.  (The first string represents the beginning of the string [the root])
        string[] tokens = new string[div.Length - 1];
        Array.Copy(div, 1, tokens, 0, tokens.Length);
        
        // Build the regex, starting with the root followed by any number of directories, then the next tokens, each with possibly multiple directories between (loose search).
        regexes[i] = BuildSearchRegex(tokens, Regex.Escape(div[0]) + @"([^\\]*\\)*", @"([^\\]*\\)+", @"[^\\]*\\$");
      }
      return regexes;
    }

    #region Data File IO
    #region Filenames
    /// <summary>
    /// The User directory belonging to CCD.
    /// </summary>
    static string AppDir
    {
      get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CCD"); }
    }
    
    /// <summary>
    /// The ignore file name.
    /// </summary>
    static string IgnoreFilename
    {
      get { return System.IO.Path.Combine(AppDir, "ignores.dat"); }
    }
    
    /// <summary>
    /// The root file name.
    /// </summary>
    static string RootFilename
    {
      get { return System.IO.Path.Combine(AppDir, "roots.dat"); }
    }
    
    /// <summary>
    /// The list filename.
    /// </summary>
    static string ListFilename
    {
      get { return System.IO.Path.Combine(AppDir, "paths.dat"); }
    }
    #endregion
    
    
    /// <summary>
    /// Write a list of strings, one per line, to a given file.
    /// </summary>
    /// <param name="filename"></param>
    /// <param name="strings"></param>
    static void WriteStrings(string filename, string[] strings)
    {
      using(StreamWriter writer = new StreamWriter(filename))
      {
        foreach(string str in strings)
        {
          writer.WriteLine(str);
        }
      }
    }
    
    
    /// <summary>
    /// Load a list of strings (one from each line) from a file.
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    static string[] LoadStrings(string filename)
    {
      List<string> strings = new List<string>();
      if(System.IO.File.Exists(filename))
      {
        using(StreamReader reader = new StreamReader(filename))
        {
          while(true)
          {
            string line = reader.ReadLine();
            if(line == null)
              break;

            strings.Add(line);
          }
        }
      }
      return strings.ToArray();
    }
    
    /// <summary>
    /// Write the strings to the paths file.
    /// </summary>
    /// <param name="paths"></param>
    static void WritePaths(string[] paths)
    {
      listMutex.WaitOne();
      WriteStrings(ListFilename, paths);
      listMutex.ReleaseMutex();
    }
    
    
    /// <summary>
    /// Load the strings from the paths file.
    /// </summary>
    /// <returns></returns>
    static string[] LoadPaths()
    {
      listMutex.WaitOne();
      string[] strings = LoadStrings(ListFilename);
      listMutex.ReleaseMutex();
      return strings;
    }
    
    
    /// <summary>
    /// Load the strings from the ignores file.
    /// </summary>
    /// <returns></returns>
    static string[] LoadIgnores()
    {
      ignoreMutex.WaitOne();
      string[] strings = LoadStrings(IgnoreFilename);
      ignoreMutex.ReleaseMutex();
      return strings;
    }
    
    
    /// <summary>
    /// Load the ignores files and keep any regexes that belong under the given roots.
    /// </summary>
    /// <param name="roots"></param>
    /// <returns></returns>
    static Regex[] LoadIgnoreRegexes(string[] roots)
    {
      List<string> ignoreStrings = new List<string>(LoadIgnores());
      
      for(int i = 0; i < ignoreStrings.Count; )
      {
        bool found = false;
        foreach(string root in roots)
        {
          string dir = ignoreStrings[i].Split(' ')[0];
          if(ignoreStrings[i].StartsWith(root) || root.StartsWith(dir))
          {
            found = true;
            break;
          }
        }
        if(!found)
          ignoreStrings.RemoveAt(i);
        else
          i++;
      }
      return BuildIgnoreRegexes(ignoreStrings.ToArray());
    }
        
    
    /// <summary>
    /// Load the ignore strings from file and convert them into regexes.
    /// </summary>
    /// <returns></returns>
    static Regex[] LoadIgnoreRegexes()
    {
      string[] strings = LoadIgnores();
      return BuildIgnoreRegexes(strings);
    }
    
    
    /// <summary>
    /// Write a list of ignore strings to the ignore file.
    /// </summary>
    /// <param name="ignores"></param>
    static void WriteIgnores(string[] ignores)
    {
      ignoreMutex.WaitOne();
      WriteStrings(IgnoreFilename, ignores);
      ignoreMutex.ReleaseMutex();
    }
    
    
    /// <summary>
    /// Write the roots to the root file.
    /// </summary>
    /// <param name="roots"></param>
    static void WriteRoots(string[] roots)
    {
      rootMutex.WaitOne();
      WriteStrings(RootFilename, roots);
      rootMutex.ReleaseMutex();
    }
    
    
    /// <summary>
    /// Load the roots from the root file.
    /// </summary>
    /// <returns></returns>
    static string[] LoadRoots()
    {
      rootMutex.WaitOne();
      string[] strings = LoadStrings(RootFilename);
      rootMutex.ReleaseMutex();
      return strings;
    }
    #endregion
    
    
    
    /// <summary>
    /// Build an ignore string from a set of tokens.
    /// </summary>
    /// <param name="ignoreParts">The tokens to build from</param>
    /// <returns>The ignore string</returns>
    static string BuildIgnore(string[] ignoreParts)
    {
      // TODO: this should probably be built to handle spaces in the individual strings
      StringBuilder builder = new StringBuilder(TrailBackslash(ignoreParts[0].ToLowerInvariant()));
      for(int i = 1; i < ignoreParts.Length; i++)
      {
        builder.Append(" ");
        builder.Append(ignoreParts[i].ToLowerInvariant());
      }
      return builder.ToString();
    }
    
    
    /// <summary>
    /// Add an ignore (specified by one or more strings) to the ignore file.
    /// </summary>
    /// <param name="addition"></param>
    static void AddIgnore(string[] addition)
    {
      // Can't add an ignore rule for a directory that doesn't exist.
      if (!Directory.Exists(addition[0]))
      {
        Console.WriteLine("Directory \"" + addition[0] + "\" does not exist");
        return;
      }
      
      ignoreMutex.WaitOne();
      {
        string[] ignores = LoadIgnores();
        string built = BuildIgnore(addition);
        
        // Ensure that the ignore isn't already in the list
        bool found = false;
        foreach(string ignore in ignores)
        {
          if(ignore == built)
          {
            Console.WriteLine("This ignore already exists in the list");
            found = true;
            break;
          }
        }
        
        if(!found)
        {
          // Build the new ignore array and write it to file
          string[] newIgnores = new string[ignores.Length+1];
          Array.Copy(ignores, 0, newIgnores, 1, ignores.Length);
          newIgnores[0] = built;
          WriteIgnores(newIgnores);      
        }
      }
      ignoreMutex.ReleaseMutex();
    }
    
    
    
    /// <summary>
    /// Remove an ignore (specified by one or more strings) from the ignore file.
    /// </summary>
    /// <param name="addition"></param>
    static void DelIgnore(string[] addition)
    {
      string built = BuildIgnore(addition);
      
      ignoreMutex.WaitOne();
      {
        List<string> ignores = new List<string>(LoadIgnores());
        bool removed = false;
        
        // Scan the list for the ignore and remove it when we find it
        for(int i = 0; i < ignores.Count; i++)
        {
          if(ignores[i] == built)
          {
            ignores.RemoveAt(i);
            WriteIgnores(ignores.ToArray());
            removed = true;
            break;
          }
        }
        
        if(!removed)
        {
          Console.WriteLine("Ignore was not in list.");
        }
      }
      ignoreMutex.ReleaseMutex();
    }
    
    
    /// <summary>
    ///  Display a text window containing all of the ignores.
    /// </summary>
    static void ListIgnores()
    {
      string[] ignores = LoadIgnores();
      if(ignores.Length == 0)
      {
        Console.WriteLine("No ignores to display.");
        return;
      }
      TextWindow.Run(ignores);
    }
     
    
    
    /// <summary>
    /// Split any args that contain backslashes into separate args.
    /// </summary>
    /// <param name="args"></param>
    /// <returns>Array of the new, separated args</returns>
    static string[] SeparateArgs(string[] args)
    {
      List<string> separatedArgs = new List<string>();
      foreach(string arg in args)
      {
        string[] separated = arg.Split('\\');
        separatedArgs.AddRange(separated);
      }
      return separatedArgs.ToArray();
    }    


    /// <summary>
    /// Ensures that the current directory is in the list if it's in a root and, if so, filters the dirs so that the
    /// searchable dir list contains only those dirs under the current root.
    /// </summary>
    /// <param name="roots"></param>
    /// <param name="dirs"></param>
    /// <param name="searchAllRoots"></param>
    /// <returns>A new list of dirs representing the dirs under the current root</returns>
    static string[] ValidateCurrentDirectory(string[] roots, string[] dirs, bool searchAllRoots)
    {
      string curDir = TrailBackslash(System.Environment.CurrentDirectory.ToLowerInvariant());
      
      foreach(string root in roots)
      {
        // If we're under this root
        if(curDir.StartsWith(root))
        {
          bool found = false;
          
          // See if the string is in the list already
          foreach(string dir in dirs)
          {
            if(curDir == dir)
            {
              found = true;
              break;
            }
          }
          if(!found)
          {
            // If it wasn't in the list, ensure we're not explicitly ignoring this directory.
            Regex[] ignores = LoadIgnoreRegexes();
            foreach(Regex ignore in ignores)
            {
              if(ignore.IsMatch(curDir))
              {
                found = true;
                break;
              }
            }
          }
          
          if(!found)
          {
            // If it wasn't in the list AND it's not explicitly ignored, do a background rescan.
            RescanBackground(root);
          }
          
          if(!searchAllRoots)
          {
            // If we're not searching all roots, remove all files except files under our root.
            List<string> newDirs = new List<string>();
            foreach(string dir in dirs)
            {
              if(dir.StartsWith(root))
                newDirs.Add(dir);
            }
            dirs = newDirs.ToArray();
          }
          break;
        }
      }
      return dirs;
    }    
    
    
    
    /// <summary>
    /// If the condition is true, display the help information.
    /// </summary>
    /// <param name="condition"></param>
    /// <returns>The same condition that was passed in</returns>
    static bool OptionalDisplayHelp(bool condition)
    {
      if(condition)
        DisplayHelp();
      return condition;
    }
    
    /// <summary>
    /// The main routine.
    /// </summary>
    /// <param name="args"></param>
    static void Main(string[] args)
    {
      if(IsBackgroundScanning())
        Console.WriteLine("Background scanning detected: database may be out of date.");
      
      List<string> matches = new List<string>();
      int maxLength = 0;
      
      // With no args, list all directories in the current root
      if(args.Length == 0)
        args = new string[1] { "" };
      
      bool searchAllRoots = false;
      switch(args[0].ToLowerInvariant())
      {
        case "-h":
        case "/q":
        case "-q":
        case "/h":
        case "/?":
        case "-?":
        case "-help":
        case "/help":
          DisplayHelp();
          return;
          
        case "-addignore":
          if(OptionalDisplayHelp(args.Length < 2))
            return;
          // Scope for variable declaration
          {
            string[] ignoreArgs = new string[args.Length-1];
            Array.Copy(args, 1, ignoreArgs, 0, ignoreArgs.Length);
            AddIgnore(ignoreArgs);
          }
          return;
          
        case "-delignore":
          if (OptionalDisplayHelp(args.Length < 2))
            return;
          // Scope for variable declaration
          {
            string[] ignoreArgs = new string[args.Length - 1];
            Array.Copy(args, 1, ignoreArgs, 0, ignoreArgs.Length);
            DelIgnore(ignoreArgs);
          }
          return;
          
        case "-listignores":
          if(OptionalDisplayHelp(args.Length != 1))
            return;
          ListIgnores();
          return;
          
        case "-addroot":
          if(OptionalDisplayHelp(args.Length != 2))
            return;
          AddRoot(args[1]);
          return;
          
        case "-delroot":
          if(OptionalDisplayHelp(args.Length != 2))
            return;
          DelRoot(args[1]);
          return;
          
        case "-listroots":
          if(OptionalDisplayHelp(args.Length != 1))
            return;
          ListRoots();
          return;
          
        case "-rescan":
          if(args.Length == 2)
          {
            if(OptionalDisplayHelp(args[1].ToLowerInvariant() != "all"))
              return;
            RescanAll();
            return;
          }
          
          else if(OptionalDisplayHelp(args.Length != 1))
            return;
          RescanCurrentRoot();
          return;
          
        case "-searchall":
        case "-a":
          if(OptionalDisplayHelp(args.Length == 1))
            return;
          // Scope for variable declaration
          {
            string[] newargs = new string[args.Length-1];
            for(int i = 0; i < newargs.Length; i++)
              newargs[i] = args[i+1];
            args = newargs;
          }
          searchAllRoots = true;
          break;
      }

      // If we get here in the code, we're doing an actual scan.
      
      // Split any args that contain backslashes.
      args = SeparateArgs(args);
      
      // Convert all args to lowercase.
      for(int i = 0; i < args.Length; i++)
        args[i] = args[i].ToLowerInvariant();
  
  
      // Load the roots and dirs.
      string[] roots = LoadRoots();
      string[] dirs = LoadPaths();
      
      // Check to see if the current directory is in a valid root and, if so, filter out the dirs that we don't need to scan.
      dirs = ValidateCurrentDirectory(roots, dirs, searchAllRoots);
            
      // Run with the "tight" regex (the regex that only finds tokens that are neighboring
      Regex rex = BuildSearchRegex(args, @"\\", @"[^\\]*\\", @"[^\\]*\\$");
      maxLength = RunTest(rex, dirs, matches);
      
      if(matches.Count == 0)
      {
        // No match on the tight regex, so use the loose test 
        // (args can have arbitrary numbers of tokens between them
        Regex rexLoose = BuildSearchRegex(args, @"\\", @"([^\\]*\\)+", @"[^\\]*\\$");
        maxLength = RunTest(rexLoose, dirs, matches);
      }
      
      if(matches.Count == 0)
      {
        // No matches at all.
        Console.WriteLine("Not found!");
        return;
      }
      
      string match;
      if (matches.Count > 1)
      {
        // We want to start with the best match (the match that is 'closest' to the current directory) selected.
        string curDir = TrailBackslash(Environment.CurrentDirectory.ToLowerInvariant());
        int bestMatchIndex = 0;
        int longestMatch = 0;
        for(int i = 0; i < matches.Count; i++)
        {
          int similar;
          
          // Test each character until we reach one that doesn't match
          int loopLength = System.Math.Min(matches[i].Length, curDir.Length);
          for(similar = 0; similar < loopLength; similar++)
          {
            if(matches[i][similar] != curDir[similar])
              break;
          }
          
          // See if we found a better match
          if(similar > longestMatch)
          {
            longestMatch = similar;
            bestMatchIndex = i;
          }
          else if(similar == longestMatch && similar != 0)
          {
            // If the length is the SAME, gravitate towards the shortest match in the list (likely the first one, but this code doesn't hurt)
            if(matches[i].Length < matches[bestMatchIndex].Length)
            {
              bestMatchIndex = i;
            }
          }
        }
        match = TextWindow.Run(matches.ToArray(), maxLength, bestMatchIndex); // For more than one match, we need the text list.
      }
      else
        match = matches[0]; // For just one argument, go ahead and change directory.
      
      if(match != null)
        ParentDirectoryChanger.Change(match);
    }
  }
}
