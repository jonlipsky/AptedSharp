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

namespace AptedSharp.String
{
    /// <summary>
    ///     This is a unit-cost model defined on strings.
    /// </summary>
    public class StringCostModel : ICostModel<string>
    {
        /// <summary>
        ///     Calculates the cost of deleting a node.
        /// </summary>
        /// <param name="n">a node considered to be deleted.</param>
        /// <returns>{@code 1} - a fixed cost of deleting a node.</returns>
        public float Delete(INode<string> n)
        {
            return 1.0f;
        }

        /// <summary>
        ///     Calculates the cost of inserting a node.
        /// </summary>
        /// <param name="n">a node considered to be inserted.</param>
        /// <returns>{@code 1} - a fixed cost of inserting a node.</returns>
        public float Insert(INode<string> n)
        {
            return 1.0f;
        }

        /// <summary>
        ///     Calculates the cost of updating the string of the source node to the string
        ///     of the destination node.
        /// </summary>
        /// <param name="n1">a source node for update.</param>
        /// <param name="n2">a destination node for update.</param>
        /// <returns>@code 1} if labels of updated nodes are equal, and {@code 0} otherwise.</returns>
        public float Update(
            INode<string> n1,
            INode<string> n2)
        {
            return Equals(n1.NodeData, n2.NodeData) ? 0.0f : 1.0f;
        }
    }
}