/* MIT License
 *
 * Copyright (c) 2017 Mateusz Pawlik
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

namespace AptedSharp
{
    /// <summary>
    /// This interface specifies the methods to implement to update operation for a tree
    /// to transform it from it's source form to it's target form
    /// 
    /// </summary>
    /// <typeparam name="T">type of node data.</typeparam>
    public interface IOperationExecutor<T>
    {
        /// <summary>
        /// Perform the deletion of a node
        /// </summary>
        /// <param name="n">the node considered to be deleted.</param>
        public void Delete(INode<T> n);

        /// <summary>
        /// Perform the insertion of a node
        /// </summary>
        /// <param name="n">the node considered to be inserted.</param>
        public void Insert(INode<T> n);

        /// <summary>
        /// Perform the updating (mapping) of two nodes.
        /// </summary>
        /// <param name="n1">the source node of update.</param>
        /// <param name="n2">the destination node of update.</param>
        public void Update(
            INode<T> n1, 
            INode<T> n2);
    }
}