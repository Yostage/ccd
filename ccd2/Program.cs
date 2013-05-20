using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace CCD2
{
    static class Program
    {
        #region Cross-process mutexes
        static System.Threading.Mutex rootMutex = new System.Threading.Mutex(false, "CCDRootMutex");
        static System.Threading.Mutex listMutex = new System.Threading.Mutex(false, "CCDListMutex");
        static System.Threading.Mutex scanMutex = new System.Threading.Mutex(false, "CCDScanMutex");
        static System.Threading.Mutex ignoreMutex = new System.Threading.Mutex(false, "CCDIgnoreMutex");
        #endregion


        /// <summary>
        /// Checks to see if there is background scanning by attempting to acquire the scan mutex.
        /// </summary>
        /// <returns>True if there is background scanning, false otherwise</returns>
        static bool IsBackgroundScanning()
        {
            if (scanMutex.WaitOne(0, false))
            {
                scanMutex.ReleaseMutex();
                return false;
            }
            return true;
        }

        /// <summary>
        /// Remove any paths from the given list that belong to the supplied root.
        /// </summary>
        /// <param name="root"></param>
        /// <param name="paths"></param>
        public static void RemoveRootPaths(string root, List<string> paths)
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
            for (int i = 0; i < paths.Count; )
            {
                bool found = false;
                foreach (string root in roots)
                {
                    if (paths[i].StartsWith(root))
                    {
                        paths.RemoveAt(i);
                        found = true;
                        break;
                    }
                }
                if (!found)
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
        public static Regex BuildSearchRegex(string[] args, string first, string middle, string end)
        {
            bool initial = true;
            StringBuilder builder = new StringBuilder(first);
            foreach (string arg in args)
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
                if (regex.IsMatch(line))
                {
                    if (line.Length > maxLength)
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
        /// Displays a list of all of the roots.
        /// </summary>
        static void ListRoots()
        {
            // Load root data (we don't need to hold the mutex here, the LoadRoots function does it
            string[] roots = DiskIndexCache.LoadRoots();
            if (roots.Length == 0)
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
            if (IsBackgroundScanning())
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
        public static void Rescan(string root)
        {
            Rescan(new string[] { root });
        }


        /// <summary>
        /// Rescan the current directory's root
        /// </summary>
        static void RescanCurrentRoot()
        {

            // Find the current directory (backslash trailed and lowercase)
            string curDir = DiskIndexCache.TrailBackslash(System.Environment.CurrentDirectory.ToLowerInvariant());

            // Scour the root list for the current directory
            string[] roots = DiskIndexCache.LoadRoots();
            foreach (string root in roots)
            {
                if (curDir.StartsWith(root))
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
            Rescan(DiskIndexCache.LoadRoots());
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
                List<string> pathList = new List<string>(DiskIndexCache.LoadPaths());

                // Remove from the path list any paths that belong to any of the given roots.
                RemoveRootPaths(roots, pathList);

                // We'll maintain a stack of the paths that need to be searched.  Start by pushing all of the roots onto it.
                Stack<string> pathsToSearch = new Stack<string>();
                for (int i = roots.Length - 1; i >= 0; i--)
                    pathsToSearch.Push(roots[i]);

                // Finally, we need the regular expressions corresponding to the ignore rules.
                Regex[] ignores = DiskIndexCache.LoadIgnoreRegexes(roots);

                System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
                watch.Start();

                const float timeDiff = 0.5f;
                float prevTime = ((float)watch.ElapsedMilliseconds) / 1000 - timeDiff - 0.1f;

                Console.WriteLine();
                int writeX = Console.WindowLeft;
                int writeY = SuperConsole.CursorY - 1;
                SuperConsole.CacheConsole(writeX, writeY, Console.WindowWidth, 2);
                SuperConsole.WriteColor(writeX, writeY, ConsoleColor.LightYellow, ConsoleColor.DarkBlue, "Rescanning...".PadRight(Console.WindowWidth));

                // For each directory
                while (pathsToSearch.Count != 0)
                {
                    // Make sure to add backslashes as necessary.
                    string path = DiskIndexCache.TrailBackslash(pathsToSearch.Pop());

                    // If this path matches any of the ignore lists, we'll skip it completely.
                    bool ignore = false;
                    for (int i = 0; i < ignores.Length; i++)
                    {
                        if (ignores[i].IsMatch(path))
                        {
                            ignore = true;
                            break;
                        }
                    }
                    if (ignore)
                        continue;

                    // If enough time has elapsed, display a new directory
                    float curTime = ((float)watch.ElapsedMilliseconds) / 1000;
                    if (curTime - prevTime > timeDiff)
                    {
                        prevTime += timeDiff;
                        SuperConsole.WriteColor(writeX, writeY + 1, ConsoleColor.White, ConsoleColor.DarkBlue, path.PadRight(Console.WindowWidth));
                        System.Threading.Thread.Sleep(1);
                    }

                    // Get the subdirectories (wrapped in a try block mostly to avoid the access denied errors that some folders give)
                    string[] newDirs;
                    try
                    {
                        newDirs = Directory.GetDirectories(path);
                    }
                    catch (Exception) { newDirs = new string[0]; }

                    // Add the lower-cased current path to the list.
                    pathList.Add(path.ToLowerInvariant());

                    // Add each subdirectory to the scan list.
                    for (int i = newDirs.Length - 1; i >= 0; i--)
                        pathsToSearch.Push(newDirs[i]);
                }
                SuperConsole.RestoreCache();
                SuperConsole.CursorY--;

                // Sort and write the path list.
                pathList.Sort();
                DiskIndexCache.WritePaths(pathList.ToArray());
            }
            scanMutex.ReleaseMutex();
        }




        /// <summary>
        ///  Display a text window containing all of the ignores.
        /// </summary>
        static void ListIgnores()
        {
            string[] ignores = DiskIndexCache.LoadIgnores();
            if (ignores.Length == 0)
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
            foreach (string arg in args)
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
            string curDir = DiskIndexCache.TrailBackslash(System.Environment.CurrentDirectory.ToLowerInvariant());

            foreach (string root in roots)
            {
                // If we're under this root
                if (curDir.StartsWith(root))
                {
                    bool found = false;

                    // See if the string is in the list already
                    foreach (string dir in dirs)
                    {
                        if (curDir == dir)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        // If it wasn't in the list, ensure we're not explicitly ignoring this directory.
                        Regex[] ignores = DiskIndexCache.LoadIgnoreRegexes();
                        foreach (Regex ignore in ignores)
                        {
                            if (ignore.IsMatch(curDir))
                            {
                                found = true;
                                break;
                            }
                        }
                    }

                    if (!found)
                    {
                        // If it wasn't in the list AND it's not explicitly ignored, do a background rescan.
                        RescanBackground(root);
                    }

                    if (!searchAllRoots)
                    {
                        // If we're not searching all roots, remove all files except files under our root.
                        List<string> newDirs = new List<string>();
                        foreach (string dir in dirs)
                        {
                            if (dir.StartsWith(root))
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
            if (condition)
                DisplayHelp();
            return condition;
        }

        /// <summary>
        /// The main routine.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            if (IsBackgroundScanning())
                Console.WriteLine("Background scanning detected: database may be out of date.");

            List<string> matches = new List<string>();
            int maxLength = 0;

            // With no args, list all directories in the current root
            if (args.Length == 0)
                args = new string[1] { "" };

            bool searchAllRoots = false;
            switch (args[0].ToLowerInvariant())
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
                    if (OptionalDisplayHelp(args.Length < 2))
                        return;
                    // Scope for variable declaration
                    {
                        string[] ignoreArgs = new string[args.Length - 1];
                        Array.Copy(args, 1, ignoreArgs, 0, ignoreArgs.Length);
                        DiskIndexCache.AddIgnore(ignoreArgs);
                    }
                    return;

                case "-delignore":
                    if (OptionalDisplayHelp(args.Length < 2))
                        return;
                    // Scope for variable declaration
                    {
                        string[] ignoreArgs = new string[args.Length - 1];
                        Array.Copy(args, 1, ignoreArgs, 0, ignoreArgs.Length);
                        DiskIndexCache.DelIgnore(ignoreArgs);
                    }
                    return;

                case "-listignores":
                    if (OptionalDisplayHelp(args.Length != 1))
                        return;
                    ListIgnores();
                    return;

                case "-addroot":
                    if (OptionalDisplayHelp(args.Length != 2))
                        return;
                    DiskIndexCache.AddRoot(args[1]);
                    return;

                case "-delroot":
                    if (OptionalDisplayHelp(args.Length != 2))
                        return;
                    DiskIndexCache.DelRoot(args[1]);
                    return;

                case "-listroots":
                    if (OptionalDisplayHelp(args.Length != 1))
                        return;
                    ListRoots();
                    return;

                case "-rescan":
                    if (args.Length == 2)
                    {
                        if (OptionalDisplayHelp(args[1].ToLowerInvariant() != "all"))
                            return;
                        RescanAll();
                        return;
                    }

                    else if (OptionalDisplayHelp(args.Length != 1))
                        return;
                    RescanCurrentRoot();
                    return;

                case "-searchall":
                case "-a":
                    if (OptionalDisplayHelp(args.Length == 1))
                        return;
                    // Scope for variable declaration
                    {
                        string[] newargs = new string[args.Length - 1];
                        for (int i = 0; i < newargs.Length; i++)
                            newargs[i] = args[i + 1];
                        args = newargs;
                    }
                    searchAllRoots = true;
                    break;
            }

            // If we get here in the code, we're doing an actual scan.

            // Split any args that contain backslashes.
            args = SeparateArgs(args);

            // Convert all args to lowercase.
            for (int i = 0; i < args.Length; i++)
                args[i] = args[i].ToLowerInvariant();


            // Load the roots and dirs.
            string[] roots = DiskIndexCache.LoadRoots();
            string[] dirs = DiskIndexCache.LoadPaths();

            // Check to see if the current directory is in a valid root and, if so, filter out the dirs that we don't need to scan.
            dirs = ValidateCurrentDirectory(roots, dirs, searchAllRoots);

            // Run with the "tight" regex (the regex that only finds tokens that are neighboring
            Regex rex = BuildSearchRegex(args, @"\\", @"[^\\]*\\", @"[^\\]*\\$");
            maxLength = RunTest(rex, dirs, matches);

            if (matches.Count == 0)
            {
                // No match on the tight regex, so use the loose test 
                // (args can have arbitrary numbers of tokens between them
                Regex rexLoose = BuildSearchRegex(args, @"\\", @"([^\\]*\\)+", @"[^\\]*\\$");
                maxLength = RunTest(rexLoose, dirs, matches);
            }

            if (matches.Count == 0)
            {
                // No matches at all.
                Console.WriteLine("Not found!");
                return;
            }

            // here's where we peel out the search function
            string match;
            if (matches.Count > 1)
            {
                // We want to start with the best match (the match that is 'closest' to the current directory) selected.
                string curDir = DiskIndexCache.TrailBackslash(Environment.CurrentDirectory.ToLowerInvariant());
                int bestMatchIndex = 0;
                int longestMatch = 0;
                for (int i = 0; i < matches.Count; i++)
                {
                    int similar;

                    // Test each character until we reach one that doesn't match
                    int loopLength = System.Math.Min(matches[i].Length, curDir.Length);
                    for (similar = 0; similar < loopLength; similar++)
                    {
                        if (matches[i][similar] != curDir[similar])
                            break;
                    }

                    // See if we found a better match
                    if (similar > longestMatch)
                    {
                        longestMatch = similar;
                        bestMatchIndex = i;
                    }
                    else if (similar == longestMatch && similar != 0)
                    {
                        // If the length is the SAME, gravitate towards the shortest match in the list (likely the first one, but this code doesn't hurt)
                        if (matches[i].Length < matches[bestMatchIndex].Length)
                        {
                            bestMatchIndex = i;
                        }
                    }
                }
                match = TextWindow.Run(matches.ToArray(), maxLength, bestMatchIndex); // For more than one match, we need the text list.
            }
            else
            {
                match = matches[0]; // For just one argument, go ahead and change directory.
            }

            if (match != null)
            { 
                ParentDirectoryChanger.Change(match);
            }
        }
    }
}
