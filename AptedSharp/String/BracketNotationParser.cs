/* MIT License
 *
 * Copyright (c) 2017 Mateusz Pawlik, Nikolaus Augsten
 * Copyright (c) 2022 Jon Lipsky
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NON-INFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.Collections.Generic;

namespace AptedSharp.String
{
    /// <summary>
    ///     Parser for the input trees in the bracket notation with a single string-value
    ///     label of type {@link StringNodeData}.
    ///     Bracket notation encodes the trees with nested parentheses, for example,
    ///     in tree {A{B{X}{Y}{F}}{C}} the root node has label A and two children with
    ///     labels B and C. Node with label B has three children with labels X, Y, F.
    /// </summary>
    public class BracketNotationParser : IInputParser<string>
    {
        /// <summary>
        ///     Parses the input tree as a string and converts it to our tree
        ///     representation using the {@link Node} class.
        /// </summary>
        /// <param name="s">input tree as string in bracket notation.</param>
        /// <returns>tree representation of the bracket notation input.</returns>
        public Node<string> FromString(string s)
        {
            s = s.SubstringBetweenIndices(s.IndexOf("{", StringComparison.Ordinal),
                s.LastIndexOf("}", StringComparison.Ordinal) + 1);
            var root = GetRoot(s);
            if (root == null)
                throw new Exception($"Unable to get a root from the value {s}");
            var node = new Node<string>(root);
            var c = GetChildren(s);
            if (c != null)
                foreach (var t in c)
                    node.AddChild(FromString(t));

            return node;
        }

        private static List<string>? GetChildren(string? s)
        {
            if (!string.IsNullOrEmpty(s) && s.StartsWith("{") && s.EndsWith("}"))
            {
                var children = new List<string>();
                var end = s.IndexOf('{', 1);
                if (end == -1)
                    return children;
                var rest = s.SubstringBetweenIndices(end, s.Length - 1);
                for (int match; rest.Length > 0 && (match = MatchingBracket(rest, 0)) != -1;)
                {
                    children.Add(rest.SubstringBetweenIndices(0, match + 1));
                    if (match + 1 < rest.Length)
                        rest = rest.Substring(match + 1);
                    else
                        rest = "";
                }

                return children;
            }

            return null;
        }

        private static int MatchingBracket(
            string? s,
            int pos)
        {
            if (s == null || pos > s.Length - 1)
                return -1;

            var open = s[pos];
            char close;
            switch (open)
            {
                case '{':
                    close = '}';
                    break;

                case '(':
                    close = ')';
                    break;

                case '[':
                    close = ']';
                    break;

                case '<':
                    close = '>';
                    break;

                default:
                    return -1;
            }

            pos++;
            int count;
            for (count = 1; count != 0 && pos < s.Length; pos++)
                if (s[pos] == open)
                    count++;
                else if (s[pos] == close)
                    count--;

            if (count != 0)
                return -1;

            return pos - 1;
        }

        private static string? GetRoot(string? s)
        {
            if (!string.IsNullOrEmpty(s) && s.StartsWith("{") && s.EndsWith("}"))
            {
                var end = s.IndexOf('{', 1);
                if (end == -1)
                    end = s.IndexOf('}', 1);
                return s.SubstringBetweenIndices(1, end);
            }

            return null;
        }
    }
}