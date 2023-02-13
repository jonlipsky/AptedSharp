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

using System.Collections.Generic;

namespace AptedSharp
{
    /// <summary>
    /// This is a recursive representation of an ordered tree. Each node stores a
    /// list of pointers to its children. The order of children is significant and
    /// must be observed while implementing a custom input parser.
    /// </summary>
    /// <typeparam name="T">the type of node data (node label).</typeparam>
    public interface INode<T>
    {
        /// <summary>
        /// Information associated to and stored at each node. This can be anything
        /// and depends on the application, for example, string label, key-value pair,
        /// list of values, etc.
        /// </summary>
        T NodeData { get; }

        INode<T>? Parent { get; }
    
        /// <summary>
        /// the list with all node's children.
        /// </summary>
        IReadOnlyList<INode<T>>? Children { get; }
    }
}