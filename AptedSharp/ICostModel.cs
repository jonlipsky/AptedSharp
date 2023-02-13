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
    /// This interface specifies the methods to implement for a custom cost model.
    /// The methods represent the costs of edit operations (delete, insert, update).
    ///
    /// If the cost function is a metric, the tree edit distance is a metric too.
    ///
    /// However, the cost function does not have to be a metric - the costs of
    /// deletion, insertion and update can be arbitrary.
    ///
    /// IMPORTANT: Mind the <b>float</b> type use for costs.
    /// 
    /// </summary>
    /// <typeparam name="T">type of node data on which the cost model is defined.</typeparam>
    public interface ICostModel<T>
    {
        /// <summary>
        /// Calculates the cost of deleting a node.
        /// </summary>
        /// <param name="n">the node considered to be deleted.</param>
        /// <returns>the cost of deleting node n.</returns>
        public float Delete(INode<T> n);

        /// <summary>
        /// Calculates the cost of inserting a node.
        /// </summary>
        /// <param name="n">the node considered to be inserted.</param>
        /// <returns>he cost of inserting node n.</returns>
        public float Insert(INode<T> n);

        /// <summary>
        /// Calculates the cost of updating (mapping) two nodes.
        /// </summary>
        /// <param name="n1">the source node of update.</param>
        /// <param name="n2">the destination node of update.</param>
        /// <returns>the cost of updating (mapping) node n1 to n2.</returns>
        public float Update(
            INode<T> n1, 
            INode<T> n2);
    }
}