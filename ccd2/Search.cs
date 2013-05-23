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
    }
}
