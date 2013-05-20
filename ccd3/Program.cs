using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace ccd
{
    class Program
    {
        private static string AppDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CCD"); } }
        private static string ListFilename { get { return Path.Combine(AppDir, "paths.txt"); } }
        private static string IgnoresFilename { get { return Path.Combine(AppDir, "ignores.txt"); } }

        const string SCAN_COMMAND = "-scan";
        const string RECURSE_COMMAND = "-recurse";
        const string ALL_COMMAND = "-all";

        static int Main(string[] args)
        {
            if (args.Length == 0 || args[0].Contains("-h") || args[0].Contains("/h") || args[0].Contains("-?") || args[0].Contains("/?"))
            {
                Console.Error.WriteLine("Usage:");
                Console.Error.WriteLine("ccd3 [{0}] [{1}] <token(s)> or ccd3 <{2}|-delroot|-addignore|-listignores> <parameter>", RECURSE_COMMAND, ALL_COMMAND, SCAN_COMMAND);
                Console.Error.WriteLine("{0}              : If searching for a token, show all matching subdirectories instead of top-most only", RECURSE_COMMAND);
                Console.Error.WriteLine("{0}                  : If searching for a token, show subdirectories outside of this enlistment root", ALL_COMMAND);
                Console.Error.WriteLine("{0} [directory]     : Indexes (or rescans) all directories under $pwd or [directory]", SCAN_COMMAND);
                Console.Error.WriteLine("-delroot <directory>  : Removes all directories from index under <directory>");
                Console.Error.WriteLine("-addignore <token>    : Removes all directories from index which contain .*\\<token>");
                Console.Error.WriteLine("-listignores          : Shows all tokens being ignored");
                return 2;
            }

            bool recurse = false;
            bool showAll = false;
            switch (args[0].ToLower())
            {
                case SCAN_COMMAND:
                    if (args.Length > 1)
                    {
                        ScanRoot(Path.GetFullPath(args[1]));
                    }
                    else
                    {
                        ScanRoot(Environment.CurrentDirectory);
                    }
                    return 2;
                case "-delroot":
                    DelRoot(Path.GetFullPath(args[1]));
                    return 2;
                case "-addignore":
                    AddIgnore(args[1]);
                    return 2;
                case "-listignores":
                    ListIgnores();
                    return 2;
                case RECURSE_COMMAND:
                case ALL_COMMAND:
                    if (args[0] == ALL_COMMAND && args[1] == RECURSE_COMMAND)
                    {
                        showAll = recurse = true;
                        args = args.Skip(2).ToArray();
                    }
                    else if (args[0] == ALL_COMMAND)
                    {
                        showAll = true;
                        args = args.Skip(1).ToArray();
                    }
                    else if (args[0] == RECURSE_COMMAND)
                    {
                        recurse = true;
                        args = args.Skip(1).ToArray();
                    }
                    goto default;
                default:
                    return SearchFor(args, recurse, showAll) ? 0 : 1;
            }
        }

        private static void ListIgnores()
        {
            if (File.Exists(IgnoresFilename))
            {
                foreach (var token in File.ReadAllLines(IgnoresFilename))
                {
                    Console.Error.WriteLine(token);
                }
            }
        }

        private static bool SearchFor(string[] args, bool recurse, bool showAll)
        {
            if (!File.Exists(ListFilename))
            {
                Console.Error.WriteLine("No directories indexed, run scanroot");
                return false;
            }

            var allMatches = new List<string>();
            foreach (var dir in File.ReadLines(ListFilename))
            {
                bool found = true;
                foreach (var arg in args)
                {
                    if (!dir.Contains(arg.ToLower()))
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                {
                    if (showAll || dir.StartsWith(Environment.GetEnvironmentVariable("SDXROOT"), StringComparison.CurrentCultureIgnoreCase))
                    {
                        allMatches.Add(dir);
                    }
                }
            }

            if (!recurse) { TrimMatches(allMatches); }

            if (allMatches.Count == 0)
            {
                Console.Error.WriteLine("No matches found");
                return false;
            }
            else if (allMatches.Count == 1)
            {
                WritePath(allMatches.Single());
                return true;
            }

            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            if (currentDir.Parent != null)
            {
                while (currentDir.Parent.Parent != null)
                {
                    currentDir = currentDir.Parent;
                }

                string currentDirName = currentDir.FullName.ToLower();
                var subMatches = allMatches.Where(dir => dir.StartsWith(currentDirName)).ToList();

                if ((subMatches.Count == 1))
                {
                    if (subMatches.Single() != currentDirName)
                    {
                        WritePath(subMatches.Single());
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (subMatches.Count > 1)
                {
                    return ChooseResult(subMatches, args);
                }
            }
            return ChooseResult(allMatches, args);
        }

        private static void WritePath(string entry)
        {
            Console.WriteLine(entry.Split(new string[] {" : "}, StringSplitOptions.RemoveEmptyEntries).First());
        }

        static void TrimMatches(List<string> matches)
        {
            for (int i = 0; i < matches.Count; i++)
            {
                matches.RemoveAll(m => m.Contains(matches[i]) && !m.Equals(matches[i]));
            }
        }

        private static bool ChooseResult(List<string> matches, string[] tokens)
        {
            var longestDir = matches.Max(dir => dir.Length);
            matches.Sort();

            var form = new Form();
            form.Text = "Choose a directory";
            form.Shown += (o, s) => form.Activate();
            form.AutoSize = true;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.Width = Screen.PrimaryScreen.WorkingArea.Width;

            var listBox = new ListBox();
            listBox.Items.AddRange(matches.ToArray());
            listBox.Dock = DockStyle.Fill;
            listBox.Font = new System.Drawing.Font(System.Drawing.FontFamily.GenericMonospace, 11);
            int fontHeight = 0;
            listBox.AutoSize = true;
            listBox.KeyDown += (o, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    form.DialogResult = DialogResult.Cancel;
                    form.Close();
                }
            };
            listBox.DoubleClick += (o, e) =>
            {
                form.DialogResult = DialogResult.OK;
                form.Close();
            };

            listBox.DrawMode = DrawMode.OwnerDrawVariable;
            listBox.MeasureItem += (s, e) =>
            {
                if (fontHeight == 0)
                {
                    fontHeight = (int)e.Graphics.MeasureString("test", listBox.Font).Height;
                }
                e.ItemHeight = fontHeight;
            };
            listBox.DrawItem += (o, e) =>
            {
                e.DrawBackground();
                string entry = listBox.Items[e.Index].ToString();
                string[] pieces = entry.Split(new string[] { " : " }, StringSplitOptions.RemoveEmptyEntries);
                var brush = e.State.HasFlag(DrawItemState.Selected) ? System.Drawing.Brushes.White : System.Drawing.Brushes.Black;
                var firstMeasure = e.Graphics.MeasureString(pieces[0], e.Font);
                var firstBounds = new System.Drawing.Rectangle(e.Bounds.X, e.Bounds.Y, (int)firstMeasure.Width + 10, e.Bounds.Height);
                e.Graphics.DrawString(pieces[0], e.Font, brush, firstBounds);
                if (pieces.Length > 1)
                {
                    brush = e.State.HasFlag(DrawItemState.Selected) ? System.Drawing.Brushes.White : System.Drawing.Brushes.Blue;
                    var secondMeasure = e.Graphics.MeasureString(pieces[1], e.Font);
                    var secondBounds = firstBounds;
                    secondBounds.X += firstBounds.Width;
                    secondBounds.Width = (int)secondMeasure.Width + 10;
                    e.Graphics.DrawString(pieces[1], e.Font, brush, secondBounds);
                }
                e.DrawFocusRectangle();
            };

            form.Controls.Add(listBox);

            listBox.SelectedIndex = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                bool allFound = true;
                foreach (var token in tokens)
                {
                    if (!Regex.IsMatch(matches[i], String.Format(@"\\{0}[\\\s]", token)))
                    {
                        allFound = false;
                    }
                }
                if (allFound)
                {
                    listBox.SelectedIndex = i;
                    break;
                }
            }

            if (form.ShowDialog() == DialogResult.OK)
            {
                WritePath(listBox.SelectedItem.ToString());
                return true;
            }
            
            return false;
        }

        private static void DelRoot(string path)
        {
            if (File.Exists(ListFilename))
            {
                path = path.ToLower();
                List<string> dirs = new List<string>(File.ReadAllLines(ListFilename));
                dirs.RemoveAll(dir => dir.StartsWith(path));

                File.WriteAllLines(ListFilename, dirs);
            }
        }

        private static void ScanRoot(string path)
        {
            DelRoot(path);

            if (!Directory.Exists(AppDir))
            {
                Directory.CreateDirectory(AppDir);
            }

            List<string> ignores = new List<string>();
            if (File.Exists(IgnoresFilename))
            {
                ignores.AddRange(File.ReadAllLines(IgnoresFilename));
            }

            using (var sw = new StreamWriter(ListFilename, true))
            {
                bool showProgress = true;
                int i = 0;
                sw.WriteLine(path.ToLower());
                foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
                {
                    string dirLower = dir.ToLower();
                    if (ignores.Exists(token => Regex.IsMatch(dirLower, String.Format(@"\\{0}\b", token))))
                    {
                        continue;
                    }

                    try
                    {
                        if (showProgress && (i++ % 10 == 0))
                        {
                            WriteProgress(dir);
                        }
                    }
                    catch (IOException)
                    {
                        showProgress = false;
                    }
                    
                    string stringToWrite = null;
                    string sourcesPath = Path.Combine(dir, "sources");
                    string projPath = Directory.GetFiles(dir, "*.csproj").FirstOrDefault();
                    
                    if (File.Exists(sourcesPath))
                    {
                        foreach (var line in File.ReadLines(sourcesPath))
                        {
                            var match = Regex.Match(line, @"TARGETNAME\s*=\s*(.+)", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                string target = match.Groups[1].Value.ToLower();
                                if (!dirLower.Contains(target))
                                {
                                    stringToWrite = string.Format("{0} : {1}", dirLower, target);
                                }
                                break;
                            }
                        }
                    }
                    else if (!String.IsNullOrEmpty(projPath))
                    {
                        foreach (var line in File.ReadLines(projPath))
                        {
                            var match = Regex.Match(line, @"<assemblyname>\s*(.*)\s*</assemblyname>", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                string target = match.Groups[1].Value.ToLower();
                                if (!dirLower.Contains(target))
                                {
                                    stringToWrite = string.Format("{0} : {1}", dirLower, target);
                                }
                                break;
                            }
                        }
                    }
                    if (stringToWrite == null)
                    {
                        sw.WriteLine(dirLower);
                    }
                    else
                    {
                        sw.WriteLine(stringToWrite);
                    }
                }
            }
            Console.Error.WriteLine();
        }

        private static void AddIgnore(string token)
        {
            if (!Directory.Exists(AppDir))
            {
                Directory.CreateDirectory(AppDir);
            }

            token = token.ToLower();
            List<string> ignores = new List<string>();
            if (File.Exists(IgnoresFilename))
            {
                ignores.AddRange(File.ReadAllLines(IgnoresFilename));
            }
            if (!ignores.Contains(token))
            {
                ignores.Add(token);
            }
            File.WriteAllLines(IgnoresFilename, ignores);

            if (File.Exists(ListFilename))
            {
                List<string> dirs = new List<string>(File.ReadAllLines(ListFilename));
                dirs.RemoveAll(dir => Regex.IsMatch(dir, String.Format(@"\\{0}\b", token)));

                File.WriteAllLines(ListFilename, dirs);
            }
        }

        private static void WriteProgress(string dir)
        {
            ConsoleColor oldColor = Console.BackgroundColor;
            Console.BackgroundColor = ConsoleColor.DarkGreen;
            Console.CursorLeft = 0;
            if (dir.Length > Console.WindowWidth - 1) dir = dir.Substring(0, Console.WindowWidth - 1);
            Console.Error.Write(dir);
            Console.Error.Write(new string(' ', Console.WindowWidth - dir.Length - 1));
            Console.BackgroundColor = oldColor;
        }
    }
}
