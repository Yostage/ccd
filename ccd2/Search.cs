using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CCD2
{
    class Search
    {
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
        public static int RunTest(Regex regex, string[] cachedStrings, List<string> matches)
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

        public static List<string> SearchDirs(List<string> searchTokens, List<string> roots, List<string> dirs, string currentDirectory, bool searchAllRoots)
        {
            // todo: don't discard bestMatchIndex
            List<string> matches = new List<string>();

            // Check to see if the current directory is in a valid root and, if so, filter out the dirs that we don't need to scan.
            // todo: fix lameness
            var dirs2 = Program.ValidateCurrentDirectory(roots.ToArray(), dirs.ToArray(), searchAllRoots);
            dirs = new List<string>(dirs2);

            // Run with the "tight" regex (the regex that only finds tokens that are neighboring
            Regex rex = Search.BuildSearchRegex(searchTokens.ToArray(), @"\\", @"[^\\]*\\", @"[^\\]*\\$");
            int maxLength = Search.RunTest(rex, dirs.ToArray(), matches);

            if (matches.Count == 0)
            {
                // No match on the tight regex, so use the loose test 
                // (args can have arbitrary numbers of tokens between them
                Regex rexLoose = Search.BuildSearchRegex(searchTokens.ToArray(), @"\\", @"([^\\]*\\)+", @"[^\\]*\\$");
                maxLength = Search.RunTest(rexLoose, dirs.ToArray(), matches);
            }

            // here's where we peel out the search function
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
                // todo - don't discard bestMatchIndex
                //match = TextWindow.ChooseUserStringFromList(matches, maxLength, bestMatchIndex); // For more than one match, we need the text list.
            }

            return matches;
        }
    }
}
