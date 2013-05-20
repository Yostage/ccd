using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CCD2
{
    class DiskIndexCache
    {
        #region Cross-process mutexes
        static System.Threading.Mutex rootMutex = new System.Threading.Mutex(false, "CCDRootMutex");
        static System.Threading.Mutex listMutex = new System.Threading.Mutex(false, "CCDListMutex");
        static System.Threading.Mutex scanMutex = new System.Threading.Mutex(false, "CCDScanMutex");
        static System.Threading.Mutex ignoreMutex = new System.Threading.Mutex(false, "CCDIgnoreMutex");
        #endregion


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

        /// <summary>
        /// Write a list of strings, one per line, to a given file.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="strings"></param>
        static void WriteStrings(string filename, string[] strings)
        {
            using (StreamWriter writer = new StreamWriter(filename))
            {
                foreach (string str in strings)
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
            if (System.IO.File.Exists(filename))
            {
                using (StreamReader reader = new StreamReader(filename))
                {
                    while (true)
                    {
                        string line = reader.ReadLine();
                        if (line == null)
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
        public static void WritePaths(string[] paths)
        {
            listMutex.WaitOne();
            WriteStrings(ListFilename, paths);
            listMutex.ReleaseMutex();
        }


        /// <summary>
        /// Load the strings from the paths file.
        /// </summary>
        /// <returns></returns>
        public static string[] LoadPaths()
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
        public static string[] LoadIgnores()
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
        public static Regex[] LoadIgnoreRegexes(string[] roots)
        {
            List<string> ignoreStrings = new List<string>(LoadIgnores());

            for (int i = 0; i < ignoreStrings.Count; )
            {
                bool found = false;
                foreach (string root in roots)
                {
                    string dir = ignoreStrings[i].Split(' ')[0];
                    if (ignoreStrings[i].StartsWith(root) || root.StartsWith(dir))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
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
        public static Regex[] LoadIgnoreRegexes()
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
        public static string[] LoadRoots()
        {
            rootMutex.WaitOne();
            string[] strings = LoadStrings(RootFilename);
            rootMutex.ReleaseMutex();
            return strings;
        }

        /// <summary>
        /// Adds a new search root to the system
        /// </summary>
        /// <param name="root"></param>
        public static void AddRoot(string root)
        {
            // Lowercase and add the backslash to the root
            root = TrailBackslash(root.ToLowerInvariant());

            // The root directory must exist to be added to the list.
            if (!Directory.Exists(root))
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
                foreach (string existingRoot in roots)
                {
                    if (existingRoot == root)
                    {
                        alreadyInList = true;
                        Console.WriteLine("Root \"" + root + "\" is already in the list");
                        break;
                    }
                }

                // If it's not already in the list
                if (!alreadyInList)
                {
                    // Copy the root array to the end of a new array
                    string[] newRoots = new string[roots.Length + 1];
                    Array.Copy(roots, 0, newRoots, 1, roots.Length);

                    // Add the new root to the beginning of the list.
                    newRoots[0] = root;
                    Array.Sort<string>(newRoots);

                    // Write them to file
                    DiskIndexCache.WriteRoots(newRoots);
                }
            }
            rootMutex.ReleaseMutex();

            if (!alreadyInList)
            {
                // If we added a new root, scan the root now.
                Program.Rescan(root);
            }
        }


        /// <summary>
        /// Deletes a search root from the system
        /// </summary>
        /// <param name="root"></param>
        public static void DelRoot(string root)
        {
            // Lowercase, backslash added
            root = TrailBackslash(root.ToLowerInvariant());
            bool removed = false;

            rootMutex.WaitOne();
            {
                // Create a list from the roots file
                List<string> rootList = new List<string>(LoadRoots());

                // If we find one to remove, remove it from the list.
                for (int i = 0; i < rootList.Count; i++)
                {
                    if (rootList[i] == root)
                    {
                        rootList.RemoveAt(i);
                        removed = true;
                        break;
                    }
                }

                if (removed)
                {
                    // Write the new roots file if modifications were made.
                    DiskIndexCache.WriteRoots(rootList.ToArray());
                }
                else
                {
                    // Tell the user nothing happened
                    Console.WriteLine("Root \"" + root + "\" is not in the list.");
                }
            }
            rootMutex.ReleaseMutex();

            if (removed)
            {
                listMutex.WaitOne();
                {
                    // Load the path data, remove all paths belonging to the removed root, and write the path data back to the file.
                    List<string> pathList = new List<string>(DiskIndexCache.LoadPaths());
                    Program.RemoveRootPaths(root, pathList);
                    DiskIndexCache.WritePaths(pathList.ToArray());
                }
                listMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Add an ignore (specified by one or more strings) to the ignore file.
        /// </summary>
        /// <param name="addition"></param>
        public static void AddIgnore(string[] addition)
        {
            // Can't add an ignore rule for a directory that doesn't exist.
            if (!Directory.Exists(addition[0]))
            {
                Console.WriteLine("Directory \"" + addition[0] + "\" does not exist");
                return;
            }

            ignoreMutex.WaitOne();
            {
                string[] ignores = DiskIndexCache.LoadIgnores();
                string built = BuildIgnore(addition);

                // Ensure that the ignore isn't already in the list
                bool found = false;
                foreach (string ignore in ignores)
                {
                    if (ignore == built)
                    {
                        Console.WriteLine("This ignore already exists in the list");
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // Build the new ignore array and write it to file
                    string[] newIgnores = new string[ignores.Length + 1];
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
        public static void DelIgnore(string[] addition)
        {
            string built = BuildIgnore(addition);

            ignoreMutex.WaitOne();
            {
                List<string> ignores = new List<string>(LoadIgnores());
                bool removed = false;

                // Scan the list for the ignore and remove it when we find it
                for (int i = 0; i < ignores.Count; i++)
                {
                    if (ignores[i] == built)
                    {
                        ignores.RemoveAt(i);
                        WriteIgnores(ignores.ToArray());
                        removed = true;
                        break;
                    }
                }

                if (!removed)
                {
                    Console.WriteLine("Ignore was not in list.");
                }
            }
            ignoreMutex.ReleaseMutex();
        }

        /// <summary>
        /// If it doesn't already have one, add a backslash to the end of the given string
        /// </summary>
        /// <param name="dir">the directory string to backslashify</param>
        /// <returns>A new string with the backslash added</returns>
        public static string TrailBackslash(string dir)
        {
            if (!dir.EndsWith("\\"))
                dir += "\\";
            return dir;
        }


        /// <summary>
        /// Create the directory if it doesn't already exist
        /// </summary>
        /// <param name="dir">The directory to potentially create</param>
        static void EnsureDirectoryExists(string dir)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
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
                regexes[i] = Program.BuildSearchRegex(tokens, Regex.Escape(div[0]) + @"([^\\]*\\)*", @"([^\\]*\\)+", @"[^\\]*\\$");
            }
            return regexes;
        }

        /// <summary>
        /// Build an ignore string from a set of tokens.
        /// </summary>
        /// <param name="ignoreParts">The tokens to build from</param>
        /// <returns>The ignore string</returns>
        static string BuildIgnore(string[] ignoreParts)
        {
            // TODO: this should probably be built to handle spaces in the individual strings
            StringBuilder builder = new StringBuilder(DiskIndexCache.TrailBackslash(ignoreParts[0].ToLowerInvariant()));
            for (int i = 1; i < ignoreParts.Length; i++)
            {
                builder.Append(" ");
                builder.Append(ignoreParts[i].ToLowerInvariant());
            }
            return builder.ToString();
        }

    }
}
