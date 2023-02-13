/* MIT License
 *
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

using System.IO;

namespace AptedSharp
{
    public static class NodeExtensions
    {
        /// <summary>
        /// Returns a string representation of the tree in bracket notation.
        /// </summary>
        /// <returns>tree in bracket notation.</returns>
        public static string ToBracketNotationString<T>(
            this INode<T> node)
        {
            var writer = new StringWriter();
            node.WriteBracketNotationString(writer);
            return writer.ToString();
        }
    
        /// <summary>
        /// Writes the node in bracket notation to a StringWriter
        /// </summary>
        private static void WriteBracketNotationString<T>(
            this INode<T> node, 
            StringWriter? existingWriter = null)
        {
            var writer = existingWriter ?? new StringWriter();
            writer.Write("{");
            writer.Write(node.NodeData);

            var children = node.Children;
            if (children != null)
            {
                foreach (var child in children)
                {
                    child.WriteBracketNotationString(writer);
                }
            }

            writer.Write("}");
        }
    
        /// <summary>
        /// Counts the number of nodes in a tree rooted at this node.
        ///
        /// This method runs in linear time in the tree size.
        /// </summary>
        /// <returns>number of nodes in the tree rooted at this node.</returns>
        public static int GetNodeCount<T>(
            this INode<T> node)
        {
            var sum = 1;
            var children = node.Children;
            if (children != null)
            {
                foreach (var child in children)
                {
                    sum += child.GetNodeCount();
                }
            }

            return sum;
        }
    }
}