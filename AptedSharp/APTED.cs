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

using System;
using System.Collections.Generic;
using System.Linq;

namespace AptedSharp
{
    /// <summary>
    /// Implements APTED algorithm [1,2].
    /// <ul>
    ///     <li>Optimal strategy with all paths.</li>
    ///     <li>Single-node single path function supports currently only unit cost.</li>
    ///     <li>Two-node single path function not included.</li>
    ///     <li>\Delta^L and \Delta^R based on Zhang and Shasha's algorithm for executing
    ///     left and right paths (as in [3]). If only left and right paths are used
    ///     in the strategy, the memory usage is reduced by one quadratic array.</li>
    ///     <li>For any other path \Delta^A from [1] is used.</li>
    /// </ul>
    /// 
    ///  References:
    ///  <ul>
    ///     <li>[1] M. Pawlik and N. Augsten. Efficient Computation of the Tree Edit
    /// Distance. ACM Transactions on Database Systems (TODS) 40(1). 2015.</li>
    ///     <li>[2] M. Pawlik and N. Augsten. Tree edit distance: Robust and memory-
    ///     efficient. Information Systems 56. 2016.</li>
    ///     <li>[3] M. Pawlik and N. Augsten. RTED: A Robust Algorithm for the Tree Edit
    /// Distance. PVLDB 5(4). 2011.</li>
    ///  </ul>
    /// </summary>
    /// <typeparam name="TCostModel">type of cost model.</typeparam>
    /// <typeparam name="T">type of node data.</typeparam>
    public class Apted<TCostModel, T> where TCostModel : ICostModel<T>
    {
        /// <summary>
        /// Identifier of left path type = {@value LEFT};
        /// </summary>
        private const byte Left = 0;

        /// <summary>
        /// Identifier of right path type = {@value RIGHT};
        /// </summary>
        private const byte Right = 1;

        /// <summary>
        /// Identifier of inner path type = {@value INNER};
        /// </summary>
        private const byte Inner = 2;

        /// <summary>
        /// Indexer of the source tree.
        /// </summary>
        private NodeIndexer<T, TCostModel> _it1 = null!;

        /// <summary>
        /// Indexer of the destination tree.
        /// </summary>
        private NodeIndexer<T, TCostModel> _it2 = null!;

        /// <summary>
        /// The size of the source input tree.
        /// </summary>
        private int _size1;

        /// <summary>
        /// The size of the destination tree.
        /// </summary>
        private int _size2;

        /// <summary>
        /// The distance matrix [1, Sections 3.4,8.2,8.3]. Used to store intermediate
        /// distances between pairs of subtrees.
        /// </summary>
        private float[][] _delta = null!;

        /// <summary>
        /// One of distance arrays to store intermediate distances in spfA.
        /// </summary>
        // TODO: Verify if other spf-local arrays are initialised within spf. If yes,
        //       move q to spf to - then, an offset has to be used to access it.
        private float[] _q = null!;

        /// <summary>
        /// Array used in the algorithm before [1]. Using it does not change the
        /// complexity.
        /// TODO: Do not use it [1, Section 8.4].
        /// </summary>
        private int[] _fn = null!;

        /// <summary>
        /// Array used in the algorithm before [1]. Using it does not change the
        /// complexity.
        /// TODO: Do not use it [1, Section 8.4].
        /// </summary>
        private int[] _ft = null!;

        /// <summary>
        /// Stores the number of sub-problems encountered while computing the distance
        /// [1, Section 10].
        /// </summary>
        private long _counter;

        /// <summary>
        /// Cost model to be used for calculating costs of edit operations.
        /// </summary>
        private TCostModel _costModel;

        /// <summary>
        /// Constructs the APTED algorithm object with the specified cost model.
        /// </summary>
        /// <param name="costModel">cost model for edit operations.</param>
        public Apted(TCostModel costModel)
        {
            _costModel = costModel;
        }

        /// <summary>
        /// Compute tree edit distance between source and destination trees using
        /// APTED algorithm [1,2].
        /// </summary>
        /// <param name="t1">source tree.</param>
        /// <param name="t2">destination tree.</param>
        /// <returns>tree edit distance.</returns>
        public float ComputeEditDistance(INode<T> t1, INode<T> t2)
        {
            // Index the nodes of both input trees.
            Init(t1, t2);
            // Determine the optimal strategy for the distance computation.
            // Use the heuristic from [2, Section 5.3].
            if (_it1.NumberOfLeftMostChildLeadNodes < _it1.NumberOfRightMostLeafNodes)
            {
                _delta = ComputeOptStrategy_PostL(_it1, _it2);
            }
            else
            {
                _delta = computeOptStrategy_postR(_it1, _it2);
            }

            // Initialise structures for distance computation.
            TedInit();
            // Compute the distance.
            return Gted(_it1, _it2);
        }

        /// <summary>
        /// This method is only for testing purpose. It computes TED with a fixed
        /// path type in the strategy to trigger execution of a specific single-path
        /// function.
        /// </summary>
        /// <param name="t1">source tree.</param>
        /// <param name="t2">destination tree.</param>
        /// <param name="spfType">single-path function to trigger (LEFT or RIGHT).</param>
        /// <returns>tree edit distance.</returns>
        public float ComputeEditDistance_spfTest(INode<T> t1, INode<T> t2, int spfType)
        {
            // Index the nodes of both input trees.
            Init(t1, t2);
            // Initialise delta array.
            _delta = new float[_size1][];
            for (var i = 0; i < _delta.Length; i++)
                _delta[i] = new float[_size2];

            // Fix a path type to trigger specific spf.
            for (var i = 0; i < _delta.Length; i++)
            {
                for (var j = 0; j < _delta[i].Length; j++)
                {
                    // Fix path type.
                    if (spfType == Left)
                    {
                        _delta[i][j] = _it1.preL_to_lld(i) + 1;
                    }
                    else if (spfType == Right)
                    {
                        _delta[i][j] = _it1.preL_to_rld(i) + 1;
                    }
                }
            }

            // Initialise structures for distance computation.
            TedInit();
            // Compute the distance.
            return Gted(_it1, _it2);
        }

        /// <summary>
        /// Initialises node indexers and stores input tree sizes.
        /// </summary>
        /// <param name="t1">source input tree.</param>
        /// <param name="t2">destination input tree.</param>
        private void Init(INode<T> t1, INode<T> t2)
        {
            _it1 = new NodeIndexer<T, TCostModel>(t1, _costModel);
            _it2 = new NodeIndexer<T, TCostModel>(t2, _costModel);
            _size1 = _it1.GetSize();
            _size2 = _it2.GetSize();
        }

        /// <summary>
        /// After the optimal strategy is computed, initialises distances of deleting
        /// and inserting subtrees without their root nodes.
        /// </summary>
        private void TedInit()
        {
            // Reset the sub-problems counter.
            _counter = 0L;
            // Initialize arrays.
            var maxSize = Math.Max(_size1, _size2) + 1;
            // TODO: Move q initialisation to spfA.
            _q = new float[maxSize];
            // TODO: Do not use fn and ft arrays [1, Section 8.4].
            _fn = new int[maxSize + 1];
            _ft = new int[maxSize + 1];
            // Compute subtree distances without the root nodes when one of subtrees
            // is a single node.
            //var parentX = -1;
            //var parentY = -1;
            // Loop over the nodes in reversed left-to-right preorder.
            for (var x = 0; x < _size1; x++)
            {
                var sizeX = _it1.Sizes[x];
                //parentX = _it1.Parents[x];
                for (var y = 0; y < _size2; y++)
                {
                    var sizeY = _it2.Sizes[y];
                    //parentY = _it2.Parents[y];
                    // Set values in delta based on the sums of deletion and insertion
                    // costs. Subtracts the costs for root nodes.
                    // In this method we don't have to verify the order of the input trees
                    // because it is equal to the original.
                    if (sizeX == 1 && sizeY == 1)
                    {
                        _delta[x][y] = 0.0f;
                    }
                    else if (sizeX == 1)
                    {
                        _delta[x][y] = _it2.PreLToSumInsCost[y] - _costModel.Insert(_it2.PreLToNode[y]); // USE COST MODEL.
                    }
                    else if (sizeY == 1)
                    {
                        _delta[x][y] = _it1.PreLToSumDelCost[x] - _costModel.Delete(_it1.PreLToNode[x]); // USE COST MODEL.
                    }
                }
            }
        }

        /// <summary>
        /// Compute the optimal strategy using left-to-right postorder traversal of
        /// the nodes [2, Algorithm 1].
        /// </summary>
        /// <param name="it1">node indexer of the source input tree.</param>
        /// <param name="it2">node indexer of the destination input tree.</param>
        /// <returns>array with the optimal strategy.</returns>
        private float[][] ComputeOptStrategy_PostL(NodeIndexer<T, TCostModel> it1, NodeIndexer<T, TCostModel> it2)
        {
            var size1 = it1.GetSize();
            var size2 = it2.GetSize();
            var strategy = new float[size1][];
            for (var i = 0; i < strategy.Length; i++)
                strategy[i] = new float[size2];
            var cost1L = new float[size1][];
            var cost1R = new float[size1][];
            var cost1I = new float[size1][];
            var cost2L = new float[size2];
            var cost2R = new float[size2];
            var cost2I = new float[size2];
            var cost2Path = new int[size2];
            var leafRow = new float[size2];
            var pathIdOffset = size1;

            var pre2Size1 = it1.Sizes;
            var pre2Size2 = it2.Sizes;
            var pre2descSum1 = it1.PreLToDescSum;
            var pre2descSum2 = it2.PreLToDescSum;
            var pre2KrSum1 = it1.PreLToKrSum;
            var pre2KrSum2 = it2.PreLToKrSum;
            var pre2RevKrSum1 = it1.PreLToRevKrSum;
            var pre2RevKrSum2 = it2.PreLToRevKrSum;
            var preLToPreR1 = it1.PreLToPreR;
            var preLToPreR2 = it2.PreLToPreR;
            var preRToPreL1 = it1.PreRToPreL;
            var preRToPreL2 = it2.PreRToPreL;
            var pre2Parent1 = it1.Parents;
            var pre2Parent2 = it2.Parents;
            var nodeTypeL1 = it1.NodeTypeL;
            var nodeTypeL2 = it2.NodeTypeL;
            var nodeTypeR1 = it1.NodeTypeR;
            var nodeTypeR2 = it2.NodeTypeR;

            var preLToPostL1 = it1.PreLToPostL;
            var preLToPostL2 = it2.PreLToPostL;

            var postLToPreL1 = it1.PostLToPreL;
            var postLToPreL2 = it2.PostLToPreL;

            int parentWPostL = -1, parentVPostL = -1;
            float[] costLpointerParentV = null!, costRpointerParentV = null!, costIpointerParentV = null!;
            float[] strategypointerParentV = null!;

            Stack<float[]> rowsToReuseL = new();
            Stack<float[]> rowsToReuseR = new();
            Stack<float[]> rowsToReuseI = new();

            for (var v = 0; v < size1; v++)
            {
                var vInPreL = postLToPreL1[v];

                var isVLeaf = it1.IsLeaf(vInPreL);
                var parentVPreL = pre2Parent1[vInPreL];

                if (parentVPreL != -1)
                {
                    parentVPostL = preLToPostL1[parentVPreL];
                }

                var strategypointerV = strategy[vInPreL];

                var sizeV = pre2Size1[vInPreL];
                var leftPathV = -(preRToPreL1[preLToPreR1[vInPreL] + sizeV - 1] + 1); // this is the left path's ID which is the leftmost leaf node: l-r_preorder(r-l_preorder(v) + |Fv| - 1)
                var rightPathV = vInPreL + sizeV - 1 + 1; // this is the right path's ID which is the rightmost leaf node: l-r_preorder(v) + |Fv| - 1
                var krSumV = pre2KrSum1[vInPreL];
                var revkrSumV = pre2RevKrSum1[vInPreL];
                var descSumV = pre2descSum1[vInPreL];

                if (isVLeaf)
                {
                    cost1L[v] = leafRow;
                    cost1R[v] = leafRow;
                    cost1I[v] = leafRow;
                    for (var i = 0; i < size2; i++)
                    {
                        strategypointerV[postLToPreL2[i]] = vInPreL;
                    }
                }

                var costLpointerV = cost1L[v];
                var costRpointerV = cost1R[v];
                var costIpointerV = cost1I[v];

                if (parentVPreL != -1 && cost1L[parentVPostL] == null)
                {
                    if (rowsToReuseL.Count == 0)
                    {
                        cost1L[parentVPostL] = new float[size2];
                        cost1R[parentVPostL] = new float[size2];
                        cost1I[parentVPostL] = new float[size2];
                    }
                    else
                    {
                        cost1L[parentVPostL] = rowsToReuseL.Pop();
                        cost1R[parentVPostL] = rowsToReuseR.Pop();
                        cost1I[parentVPostL] = rowsToReuseI.Pop();
                    }
                }

                if (parentVPreL != -1)
                {
                    costLpointerParentV = cost1L[parentVPostL];
                    costRpointerParentV = cost1R[parentVPostL];
                    costIpointerParentV = cost1I[parentVPostL];
                    strategypointerParentV = strategy[parentVPreL];
                }

                Array.Fill(cost2L, 0L);
                Array.Fill(cost2R, 0L);
                Array.Fill(cost2I, 0L);
                Array.Fill(cost2Path, 0);

                for (var w = 0; w < size2; w++)
                {
                    var wInPreL = postLToPreL2[w];

                    var parentWPreL = pre2Parent2[wInPreL];
                    if (parentWPreL != -1)
                    {
                        parentWPostL = preLToPostL2[parentWPreL];
                    }

                    var sizeW = pre2Size2[wInPreL];
                    if (it2.IsLeaf(wInPreL))
                    {
                        cost2L[w] = 0L;
                        cost2R[w] = 0L;
                        cost2I[w] = 0L;
                        cost2Path[w] = wInPreL;
                    }

                    float minCost = 0x7fffffffffffffffL;
                    var strategyPath = -1;
                    float tmpCost = 0x7fffffffffffffffL;

                    if (sizeV <= 1 || sizeW <= 1)
                    {
                        // USE NEW SINGLE_PATH FUNCTIONS FOR SMALL SUBTREES
                        minCost = Math.Max(sizeV, sizeW);
                    }
                    else
                    {
                        tmpCost = sizeV * (float)pre2KrSum2[wInPreL] + costLpointerV[w];
                        if (tmpCost < minCost)
                        {
                            minCost = tmpCost;
                            strategyPath = leftPathV;
                        }

                        tmpCost = sizeV * (float)pre2RevKrSum2[wInPreL] + costRpointerV[w];
                        if (tmpCost < minCost)
                        {
                            minCost = tmpCost;
                            strategyPath = rightPathV;
                        }

                        tmpCost = sizeV * (float)pre2descSum2[wInPreL] + costIpointerV[w];
                        if (tmpCost < minCost)
                        {
                            minCost = tmpCost;
                            strategyPath = (int)strategypointerV[wInPreL] + 1;
                        }

                        tmpCost = sizeW * (float)krSumV + cost2L[w];
                        if (tmpCost < minCost)
                        {
                            minCost = tmpCost;
                            strategyPath = -(preRToPreL2[preLToPreR2[wInPreL] + sizeW - 1] + pathIdOffset + 1);
                        }

                        tmpCost = sizeW * (float)revkrSumV + cost2R[w];
                        if (tmpCost < minCost)
                        {
                            minCost = tmpCost;
                            strategyPath = wInPreL + sizeW - 1 + pathIdOffset + 1;
                        }

                        tmpCost = sizeW * (float)descSumV + cost2I[w];
                        if (tmpCost < minCost)
                        {
                            minCost = tmpCost;
                            strategyPath = cost2Path[w] + pathIdOffset + 1;
                        }
                    }

                    if (parentVPreL != -1)
                    {
                        costRpointerParentV[w] += minCost;
                        tmpCost = -minCost + cost1I[v][w];
                        if (tmpCost < cost1I[parentVPostL][w])
                        {
                            costIpointerParentV[w] = tmpCost;
                            strategypointerParentV[wInPreL] = strategypointerV[wInPreL];
                        }

                        if (nodeTypeR1[vInPreL])
                        {
                            costIpointerParentV[w] += costRpointerParentV[w];
                            costRpointerParentV[w] += costRpointerV[w] - minCost;
                        }

                        if (nodeTypeL1[vInPreL])
                        {
                            costLpointerParentV[w] += costLpointerV[w];
                        }
                        else
                        {
                            costLpointerParentV[w] += minCost;
                        }
                    }

                    if (parentWPreL != -1)
                    {
                        cost2R[parentWPostL] += minCost;
                        tmpCost = -minCost + cost2I[w];
                        if (tmpCost < cost2I[parentWPostL])
                        {
                            cost2I[parentWPostL] = tmpCost;
                            cost2Path[parentWPostL] = cost2Path[w];
                        }

                        if (nodeTypeR2[wInPreL])
                        {
                            cost2I[parentWPostL] += cost2R[parentWPostL];
                            cost2R[parentWPostL] += cost2R[w] - minCost;
                        }

                        if (nodeTypeL2[wInPreL])
                        {
                            cost2L[parentWPostL] += cost2L[w];
                        }
                        else
                        {
                            cost2L[parentWPostL] += minCost;
                        }
                    }

                    strategypointerV[wInPreL] = strategyPath;
                }

                if (!it1.IsLeaf(vInPreL))
                {
                    Array.Fill(cost1L[v], 0);
                    Array.Fill(cost1R[v], 0);
                    Array.Fill(cost1I[v], 0);
                    rowsToReuseL.Push(cost1L[v]);
                    rowsToReuseR.Push(cost1R[v]);
                    rowsToReuseI.Push(cost1I[v]);
                }
            }

            return strategy;
        }

        /// <summary>
        /// Compute the optimal strategy using right-to-left postorder traversal of
        /// the nodes [2, Algorithm 1].
        /// QUESTION: Is it possible to merge it with the other strategy computation?
        /// TODO: Document the internals. Point to lines of the algorithm.
        /// </summary>
        /// <param name="it1">node indexer of the source input tree.</param>
        /// <param name="it2">node indexer of the destination input tree.</param>
        /// <returns>array with the optimal strategy.</returns>
        private float[][] computeOptStrategy_postR(NodeIndexer<T, TCostModel> it1, NodeIndexer<T, TCostModel> it2)
        {
            var size1 = it1.GetSize();
            var size2 = it2.GetSize();
            float[][] strategy = new float[size1][];
            for (var i = 0; i < strategy.Length; i++)
                strategy[i] = new float[size2];
            float[][] cost1L = new float[size1][];
            float[][] cost1R = new float[size1][];
            float[][] cost1I = new float[size1][];
            var cost2L = new float[size2];
            var cost2R = new float[size2];
            var cost2I = new float[size2];
            var cost2Path = new int[size2];
            var leafRow = new float[size2];
            var pathIdOffset = size1;

            var pre2Size1 = it1.Sizes;
            var pre2Size2 = it2.Sizes;
            var pre2descSum1 = it1.PreLToDescSum;
            var pre2descSum2 = it2.PreLToDescSum;
            var pre2KrSum1 = it1.PreLToKrSum;
            var pre2KrSum2 = it2.PreLToKrSum;
            var pre2RevkrSum1 = it1.PreLToRevKrSum;
            var pre2RevkrSum2 = it2.PreLToRevKrSum;
            var preLToPreR1 = it1.PreLToPreR;
            var preLToPreR2 = it2.PreLToPreR;
            var preRToPreL1 = it1.PreRToPreL;
            var preRToPreL2 = it2.PreRToPreL;
            var pre2Parent1 = it1.Parents;
            var pre2Parent2 = it2.Parents;
            var nodeTypeL1 = it1.NodeTypeL;
            var nodeTypeL2 = it2.NodeTypeL;
            var nodeTypeR1 = it1.NodeTypeR;
            var nodeTypeR2 = it2.NodeTypeR;

            float[] costLPointerParentV = null!, costRPointerParentV = null!, costIPointerParentV = null!;
            float[] strategyPointerParentV = null!;

            Stack<float[]> rowsToReuseL = new();
            Stack<float[]> rowsToReuseR = new();
            Stack<float[]> rowsToReuseI = new();

            for (var v = size1 - 1; v >= 0; v--)
            {
                var isVLeaf = it1.IsLeaf(v);
                var parentV = pre2Parent1[v];

                var strategyPointerV = strategy[v];

                var sizeV = pre2Size1[v];
                var leftPathV = -(preRToPreL1[preLToPreR1[v] + pre2Size1[v] - 1] + 1); // this is the left path's ID which is the leftmost leaf node: l-r_preorder(r-l_preorder(v) + |Fv| - 1)
                var rightPathV = v + pre2Size1[v] - 1 + 1; // this is the right path's ID which is the rightmost leaf node: l-r_preorder(v) + |Fv| - 1
                var krSumV = pre2KrSum1[v];
                var revkrSumV = pre2RevkrSum1[v];
                var descSumV = pre2descSum1[v];

                if (isVLeaf)
                {
                    cost1L[v] = leafRow;
                    cost1R[v] = leafRow;
                    cost1I[v] = leafRow;
                    for (var i = 0; i < size2; i++)
                    {
                        strategyPointerV[i] = v;
                    }
                }

                var costLpointerV = cost1L[v];
                var costRpointerV = cost1R[v];
                var costIpointerV = cost1I[v];

                if (parentV != -1 && cost1L[parentV] == null)
                {
                    if (rowsToReuseL.Count == 0)
                    {
                        cost1L[parentV] = new float[size2];
                        cost1R[parentV] = new float[size2];
                        cost1I[parentV] = new float[size2];
                    }
                    else
                    {
                        cost1L[parentV] = rowsToReuseL.Pop();
                        cost1R[parentV] = rowsToReuseR.Pop();
                        cost1I[parentV] = rowsToReuseI.Pop();
                    }
                }

                if (parentV != -1)
                {
                    costLPointerParentV = cost1L[parentV];
                    costRPointerParentV = cost1R[parentV];
                    costIPointerParentV = cost1I[parentV];
                    strategyPointerParentV = strategy[parentV];
                }

                Array.Fill(cost2L, 0L);
                Array.Fill(cost2R, 0L);
                Array.Fill(cost2I, 0L);
                Array.Fill(cost2Path, 0);
                for (var w = size2 - 1; w >= 0; w--)
                {
                    var sizeW = pre2Size2[w];
                    if (it2.IsLeaf(w))
                    {
                        cost2L[w] = 0L;
                        cost2R[w] = 0L;
                        cost2I[w] = 0L;
                        cost2Path[w] = w;
                    }

                    float minCost = 0x7fffffffffffffffL;
                    var strategyPath = -1;
                    float tmpCost = 0x7fffffffffffffffL;

                    if (sizeV <= 1 || sizeW <= 1)
                    {
                        // USE NEW SINGLE_PATH FUNCTIONS FOR SMALL SUBTREES
                        minCost = Math.Max(sizeV, sizeW);
                    }
                    else
                    {
                        tmpCost = sizeV * (float)pre2KrSum2[w] + costLpointerV[w];
                        if (tmpCost < minCost)
                        {
                            minCost = tmpCost;
                            strategyPath = leftPathV;
                        }

                        tmpCost = sizeV * (float)pre2RevkrSum2[w] + costRpointerV[w];
                        if (tmpCost < minCost)
                        {
                            minCost = tmpCost;
                            strategyPath = rightPathV;
                        }

                        tmpCost = sizeV * (float)pre2descSum2[w] + costIpointerV[w];
                        if (tmpCost < minCost)
                        {
                            minCost = tmpCost;
                            strategyPath = (int)strategyPointerV[w] + 1;
                        }

                        tmpCost = sizeW * (float)krSumV + cost2L[w];
                        if (tmpCost < minCost)
                        {
                            minCost = tmpCost;
                            strategyPath = -(preRToPreL2[preLToPreR2[w] + sizeW - 1] + pathIdOffset + 1);
                        }

                        tmpCost = sizeW * (float)revkrSumV + cost2R[w];
                        if (tmpCost < minCost)
                        {
                            minCost = tmpCost;
                            strategyPath = w + sizeW - 1 + pathIdOffset + 1;
                        }

                        tmpCost = sizeW * (float)descSumV + cost2I[w];
                        if (tmpCost < minCost)
                        {
                            minCost = tmpCost;
                            strategyPath = cost2Path[w] + pathIdOffset + 1;
                        }
                    }

                    if (parentV != -1)
                    {
                        costLPointerParentV[w] += minCost;
                        tmpCost = -minCost + cost1I[v][w];
                        if (tmpCost < cost1I[parentV][w])
                        {
                            costIPointerParentV[w] = tmpCost;
                            strategyPointerParentV[w] = strategyPointerV[w];
                        }

                        if (nodeTypeL1[v])
                        {
                            costIPointerParentV[w] += costLPointerParentV[w];
                            costLPointerParentV[w] += costLpointerV[w] - minCost;
                        }

                        if (nodeTypeR1[v])
                        {
                            costRPointerParentV[w] += costRpointerV[w];
                        }
                        else
                        {
                            costRPointerParentV[w] += minCost;
                        }
                    }

                    var parentW = pre2Parent2[w];
                    if (parentW != -1)
                    {
                        cost2L[parentW] += minCost;
                        tmpCost = -minCost + cost2I[w];
                        if (tmpCost < cost2I[parentW])
                        {
                            cost2I[parentW] = tmpCost;
                            cost2Path[parentW] = cost2Path[w];
                        }

                        if (nodeTypeL2[w])
                        {
                            cost2I[parentW] += cost2L[parentW];
                            cost2L[parentW] += cost2L[w] - minCost;
                        }

                        if (nodeTypeR2[w])
                        {
                            cost2R[parentW] += cost2R[w];
                        }
                        else
                        {
                            cost2R[parentW] += minCost;
                        }
                    }

                    strategyPointerV[w] = strategyPath;
                }

                if (!it1.IsLeaf(v))
                {
                    Array.Fill(cost1L[v], 0);
                    Array.Fill(cost1R[v], 0);
                    Array.Fill(cost1I[v], 0);
                    rowsToReuseL.Push(cost1L[v]);
                    rowsToReuseR.Push(cost1R[v]);
                    rowsToReuseI.Push(cost1I[v]);
                }
            }

            return strategy;
        }

        /// <summary>
        /// Implements spf1 single path function for the case when one of the subtrees
        /// is a single node [2, Section 6.1, Algorithm 2].
        /// 
        /// We allow an arbitrary cost model which in principle may allow updates to
        /// have a lower cost than the respective deletion plus insertion. Thus,
        /// Formula 4 in [2] has to be modified to account for that case.
        /// 
        /// In this method we don't have to verify if input subtrees have been
        /// swapped because they're always passed in the original input order.
        /// </summary>
        /// <param name="ni1">node indexer for the source input subtree.</param>
        /// <param name="subtreeRootNode1">root node of a subtree in the source input tree.</param>
        /// <param name="ni2">node indexer for the destination input subtree.</param>
        /// <param name="subtreeRootNode2">root node of a subtree in the destination input tree.</param>
        /// <returns>the tree edit distance between two subtrees of the source and destination input subtrees.</returns>
        // TODO: Merge the initialisation loop in tedInit with this method.
        //       Currently, spf1 doesn't have to store distances in delta, because
        //       all of them have been stored in tedInit.
        private float Spf1(NodeIndexer<T, TCostModel> ni1, int subtreeRootNode1, NodeIndexer<T, TCostModel> ni2, int subtreeRootNode2)
        {
            var subtreeSize1 = ni1.Sizes[subtreeRootNode1];
            var subtreeSize2 = ni2.Sizes[subtreeRootNode2];
            if (subtreeSize1 == 1 && subtreeSize2 == 1)
            {
                var n1 = ni1.PreLToNode[subtreeRootNode1];
                var n2 = ni2.PreLToNode[subtreeRootNode2];
                var maxCost = _costModel.Delete(n1) + _costModel.Insert(n2);
                var renCost = _costModel.Update(n1, n2);
                return renCost < maxCost ? renCost : maxCost;
            }

            if (subtreeSize1 == 1)
            {
                var n1 = ni1.PreLToNode[subtreeRootNode1];
                var cost = ni2.PreLToSumInsCost[subtreeRootNode2];
                var maxCost = cost + _costModel.Delete(n1);
                var minRenMinusIns = cost;
                for (var i = subtreeRootNode2; i < subtreeRootNode2 + subtreeSize2; i++)
                {
                    INode<T> n2 = ni2.PreLToNode[i];
                    var nodeRenMinusIns = _costModel.Update(n1, n2) - _costModel.Insert(n2);
                    if (nodeRenMinusIns < minRenMinusIns)
                    {
                        minRenMinusIns = nodeRenMinusIns;
                    }
                }

                cost += minRenMinusIns;
                return cost < maxCost ? cost : maxCost;
            }

            if (subtreeSize2 == 1)
            {
                var n2 = ni2.PreLToNode[subtreeRootNode2];
                var cost = ni1.PreLToSumDelCost[subtreeRootNode1];
                var maxCost = cost + _costModel.Insert(n2);
                var minRenMinusDel = cost;
                for (var i = subtreeRootNode1; i < subtreeRootNode1 + subtreeSize1; i++)
                {
                    INode<T> n1 = ni1.PreLToNode[i];
                    var nodeRenMinusDel = _costModel.Update(n1, n2) - _costModel.Delete(n1);
                    if (nodeRenMinusDel < minRenMinusDel)
                    {
                        minRenMinusDel = nodeRenMinusDel;
                    }
                }

                cost += minRenMinusDel;
                return cost < maxCost ? cost : maxCost;
            }

            return -1;
        }

        /// <summary>
        /// Implements GTED algorithm [1, Section 3.4].
        /// </summary>
        /// <param name="it1">node indexer for the source input tree.</param>
        /// <param name="it2">node indexer for the destination input tree.</param>
        /// <returns>the tree edit distance between the source and destination trees.</returns>
        // TODO: Document the internals. Point to lines of the algorithm.x
        private float Gted(NodeIndexer<T, TCostModel> it1, NodeIndexer<T, TCostModel> it2)
        {
            var currentSubtree1 = it1.GetCurrentNode();
            var currentSubtree2 = it2.GetCurrentNode();
            var subtreeSize1 = it1.Sizes[currentSubtree1];
            var subtreeSize2 = it2.Sizes[currentSubtree2];

            // Use spf1.
            if ((subtreeSize1 == 1 || subtreeSize2 == 1))
            {
                return Spf1(it1, currentSubtree1, it2, currentSubtree2);
            }

            var strategyPathId = (int)_delta[currentSubtree1][currentSubtree2];

            byte strategyPathType;
            var currentPathNode = Math.Abs(strategyPathId) - 1;
            var pathIdOffset = it1.GetSize();

            var parent = -1;
            if (currentPathNode < pathIdOffset)
            {
                strategyPathType = GetStrategyPathType(strategyPathId, pathIdOffset, it1, currentSubtree1, subtreeSize1);
                while ((parent = it1.Parents[currentPathNode]) >= currentSubtree1)
                {
                    int[] ai;
                    var k = (ai = it1.Children[parent]).Length;
                    for (var i = 0; i < k; i++)
                    {
                        var child = ai[i];
                        if (child != currentPathNode)
                        {
                            it1.SetCurrentNode(child);
                            Gted(it1, it2);
                        }
                    }

                    currentPathNode = parent;
                }

                // TODO: Move this property away from node indexer and pass directly to spfs.
                it1.SetCurrentNode(currentSubtree1);

                // Pass to spfs a bool that says says if the order of input subtrees
                // has been swapped compared to the order of the initial input trees.
                // Used for accessing delta array and deciding on the edit operation
                // [1, Section 3.4].
                if (strategyPathType == 0)
                {
                    return SpfL(it1, it2, false);
                }

                if (strategyPathType == 1)
                {
                    return SpfR(it1, it2, false);
                }

                return SpfA(it1, it2, Math.Abs(strategyPathId) - 1, strategyPathType, false);
            }

            currentPathNode -= pathIdOffset;
            strategyPathType = GetStrategyPathType(strategyPathId, pathIdOffset, it2, currentSubtree2, subtreeSize2);
            while ((parent = it2.Parents[currentPathNode]) >= currentSubtree2)
            {
                int[] ai1;
                var l = (ai1 = it2.Children[parent]).Length;
                for (var j = 0; j < l; j++)
                {
                    var child = ai1[j];
                    if (child != currentPathNode)
                    {
                        it2.SetCurrentNode(child);
                        Gted(it1, it2);
                    }
                }

                currentPathNode = parent;
            }

            // TODO: Move this property away from node indexer and pass directly to spfs.
            it2.SetCurrentNode(currentSubtree2);

            // Pass to spfs a bool that says says if the order of input subtrees
            // has been swapped compared to the order of the initial input trees. Used
            // for accessing delta array and deciding on the edit operation
            // [1, Section 3.4].
            if (strategyPathType == 0)
            {
                return SpfL(it2, it1, true);
            }

            if (strategyPathType == 1)
            {
                return SpfR(it2, it1, true);
            }

            return SpfA(it2, it1, Math.Abs(strategyPathId) - pathIdOffset - 1, strategyPathType, true);
        }

        /// <summary>
        /// Implements the single-path function spfA. Here, we use it strictly for
        /// inner paths (spfL and spfR have better performance for leaft and right
        /// paths, respectively) [1, Sections 7 and 8]. However, in this stage it
        /// also executes correctly for left and right paths.
        /// </summary>
        /// <param name="it1">indexer of the left-hand input subtree.</param>
        /// <param name="it2">indexer of the right-hand input subtree.</param>
        /// <param name="pathId">the left-to-right preorder id of the strategy path's leaf node.</param>
        /// <param name="pathType">type of the strategy path (LEFT, RIGHT, INNER).</param>
        /// <param name="treesSwapped">says if the order of input subtrees has been swapped compared to the order of the initial input trees. Used for accessing delta array and deciding on the edit operation.</param>
        /// <returns>tree edit distance between left-hand and right-hand input subtrees.</returns>
        // TODO: Document the internals. Point to lines of the algorithm.
        // The implementation has been micro-tuned: variables initialised once,
        // pointers to arrays precomputed and fixed for entire lower-level loops,
        // parts of lower-level loops that don't change moved to upper-level loops.
        private float SpfA(NodeIndexer<T, TCostModel> it1, NodeIndexer<T, TCostModel> it2, int pathId, byte pathType, bool treesSwapped)
        {
            var it2Nodes = it2.PreLToNode;
            var it1Sizes = it1.Sizes;
            var it2Sizes = it2.Sizes;
            var it1Parents = it1.Parents;
            var it2Parents = it2.Parents;
            var it1PreLToPreR = it1.PreLToPreR;
            var it2PreLToPreR = it2.PreLToPreR;
            var it1PreRToPreL = it1.PreRToPreL;
            var it2PreRToPreL = it2.PreRToPreL;
            var currentSubtreePreL1 = it1.GetCurrentNode();
            var currentSubtreePreL2 = it2.GetCurrentNode();

            // Variables to incrementally sum up the forest sizes.
            var currentForestSize1 = 0;
            var currentForestSize2 = 0;
            var tmpForestSize1 = 0;
            // Variables to incrementally sum up the forest cost.
            float currentForestCost1 = 0;
            float currentForestCost2 = 0;
            float tmpForestCost1 = 0;

            var subtreeSize2 = it2.Sizes[currentSubtreePreL2];
            var subtreeSize1 = it1.Sizes[currentSubtreePreL1];
            float[][] t = new float[subtreeSize2 + 1][];
            for (var i = 0; i < t.Length; i++)
                t[i] = new float[subtreeSize2 + 1];
            float[][] s = new float[subtreeSize1 + 1][];
            for (var i = 0; i < s.Length; i++)
                s[i] = new float[subtreeSize2 + 1];
            float minCost = -1;
            // sp1, sp2 and sp3 correspond to three elements of the minimum in the
            // recursive formula [1, Figure 12].
            float sp1 = 0;
            float sp2 = 0;
            float sp3 = 0;
            var startPathNode = -1;
            var endPathNode = pathId;
            var it1PreLoff = endPathNode;
            var it2PreLoff = currentSubtreePreL2;
            var it1PreRoff = it1PreLToPreR[endPathNode];
            var it2PreRoff = it2PreLToPreR[it2PreLoff];
            // variable declarations which were inside the loops
            int lFfirst,
                rFfirst,
                rGlast,
                rGfirst,
                lGfirst,
                rGInPreL,
                lGlast;
            bool leftPart,
                rightPart,
                fForestIsTree;
            bool rFIsConsecutiveNodeOfCurrentPathNode,
                rFIsRightSiblingOfCurrentPathNode;
            float[]? sp1Spointer, sp2Spointer, sp3Spointer, sp3deltapointer, swritepointer;
            // These variables store the id of the source (which array) of looking up
            // elements of the minimum in the recursive formula [1, Figures 12,13].
            byte sp1Source, sp3Source;
            // Loop A [1, Algorithm 3] - walk up the path.
            while (endPathNode >= currentSubtreePreL1)
            {
                it1PreLoff = endPathNode;
                it1PreRoff = it1PreLToPreR[endPathNode];
                var rFlast = -1;
                var lFlast = -1;
                var endPathNodeInPreR = it1PreLToPreR[endPathNode];
                var startPathNodeInPreR = startPathNode == -1 ? 0x7fffffff : it1PreLToPreR[startPathNode];
                var parentOfEndPathNode = it1Parents[endPathNode];
                var parentOfEndPathNodeInPreR = parentOfEndPathNode == -1 ? 0x7fffffff : it1PreLToPreR[parentOfEndPathNode];
                if (startPathNode - endPathNode > 1)
                {
                    leftPart = true;
                }
                else
                {
                    leftPart = false;
                }

                if (startPathNode >= 0 && startPathNodeInPreR - endPathNodeInPreR > 1)
                {
                    rightPart = true;
                }
                else
                {
                    rightPart = false;
                }

                // Deal with nodes to the left of the path.
                if (pathType == 1 || pathType == 2 && leftPart)
                {
                    if (startPathNode == -1)
                    {
                        rFfirst = endPathNodeInPreR;
                        lFfirst = endPathNode;
                    }
                    else
                    {
                        rFfirst = startPathNodeInPreR;
                        lFfirst = startPathNode - 1;
                    }

                    if (!rightPart)
                    {
                        rFlast = endPathNodeInPreR;
                    }

                    rGlast = it2PreLToPreR[currentSubtreePreL2];
                    rGfirst = (rGlast + subtreeSize2) - 1;
                    lFlast = rightPart ? endPathNode + 1 : endPathNode;
                    _fn[_fn.Length - 1] = -1;
                    for (var i = currentSubtreePreL2; i < currentSubtreePreL2 + subtreeSize2; i++)
                    {
                        _fn[i] = -1;
                        _ft[i] = -1;
                    }

                    // Store the current size and cost of forest in F.
                    tmpForestSize1 = currentForestSize1;
                    tmpForestCost1 = currentForestCost1;
                    // Loop B [1, Algorithm 3] - for all nodes in G (right-hand input tree).
                    for (var rG = rGfirst; rG >= rGlast; rG--)
                    {
                        lGfirst = it2PreRToPreL[rG];
                        rGInPreL = it2PreRToPreL[rG];
                        var rGminus1InPreL = rG <= it2PreLToPreR[currentSubtreePreL2] ? 0x7fffffff : it2PreRToPreL[rG - 1];
                        var parentOfRGInPreL = it2Parents[rGInPreL];
                        // This if statement decides on the last lG node for Loop D [1, Algorithm 3];
                        if (pathType == 1)
                        {
                            if (lGfirst == currentSubtreePreL2 || rGminus1InPreL != parentOfRGInPreL)
                            {
                                lGlast = lGfirst;
                            }
                            else
                            {
                                lGlast = it2Parents[lGfirst] + 1;
                            }
                        }
                        else
                        {
                            lGlast = lGfirst == currentSubtreePreL2 ? lGfirst : currentSubtreePreL2 + 1;
                        }

                        UpdateFnArray(it2.PreLToLn[lGfirst], lGfirst, currentSubtreePreL2);
                        UpdateFtArray(it2.PreLToLn[lGfirst], lGfirst);
                        var rF = rFfirst;
                        // Reset size and cost of the forest in F.
                        currentForestSize1 = tmpForestSize1;
                        currentForestCost1 = tmpForestCost1;
                        // Loop C [1, Algorithm 3] - for all nodes to the left of the path node.
                        for (var lF = lFfirst; lF >= lFlast; lF--)
                        {
                            // This if statement fixes rF node.
                            if (lF == lFlast && !rightPart)
                            {
                                rF = rFlast;
                            }

                            INode<T> lFNode = it1.PreLToNode[lF];
                            // Increment size and cost of F forest by node lF.
                            currentForestSize1++;
                            currentForestCost1 += (treesSwapped ? _costModel.Insert(lFNode) : _costModel.Delete(lFNode)); // USE COST MODEL - sum up deletion cost of a forest.
                            // Reset size and cost of forest in G to subtree G_lGfirst.
                            currentForestSize2 = it2Sizes[lGfirst];
                            currentForestCost2 = (treesSwapped ? it2.PreLToSumDelCost[lGfirst] : it2.PreLToSumInsCost[lGfirst]); // USE COST MODEL - reset to subtree insertion cost.
                            var lFInPreR = it1PreLToPreR[lF];
                            fForestIsTree = lFInPreR == rF;
                            var lFSubtreeSize = it1Sizes[lF];
                            var lFIsConsecutiveNodeOfCurrentPathNode = startPathNode - lF == 1;
                            var lFIsLeftSiblingOfCurrentPathNode = lF + lFSubtreeSize == startPathNode;
                            sp1Spointer = s[(lF + 1) - it1PreLoff];
                            sp2Spointer = s[lF - it1PreLoff];
                            sp3Spointer = s[0];
                            sp3deltapointer = treesSwapped ? null : _delta[lF];
                            swritepointer = s[lF - it1PreLoff];
                            sp1Source = 1; // Search sp1 value in s array by default.
                            sp3Source = 1; // Search second part of sp3 value in s array by default.
                            if (fForestIsTree)
                            {
                                // F_{lF,rF} is a tree.
                                if (lFSubtreeSize == 1)
                                {
                                    // F_{lF,rF} is a single node.
                                    sp1Source = 3;
                                }
                                else if (lFIsConsecutiveNodeOfCurrentPathNode)
                                {
                                    // F_{lF,rF}-lF is the path node subtree.
                                    sp1Source = 2;
                                }

                                sp3 = 0;
                                sp3Source = 2;
                            }
                            else
                            {
                                if (lFIsConsecutiveNodeOfCurrentPathNode)
                                {
                                    sp1Source = 2;
                                }

                                sp3 = currentForestCost1 - (treesSwapped ? it1.PreLToSumInsCost[lF] : it1.PreLToSumDelCost[lF]); // USE COST MODEL - Delete F_{lF,rF}-F_lF.
                                if (lFIsLeftSiblingOfCurrentPathNode)
                                {
                                    sp3Source = 3;
                                }
                            }

                            if (sp3Source == 1)
                            {
                                sp3Spointer = s[(lF + lFSubtreeSize) - it1PreLoff];
                            }

                            // Go to first lG.
                            var lG = lGfirst;
                            // currentForestSize2++;
                            // sp1, sp2, sp3 -- Done here for the first node in Loop D. It differs for consecutive nodes.
                            // sp1 -- START
                            switch (sp1Source)
                            {
                                case 1:
                                    sp1 = sp1Spointer[lG - it2PreLoff];
                                    break;
                                case 2:
                                    sp1 = t[lG - it2PreLoff][rG - it2PreRoff];
                                    break;
                                case 3:
                                    sp1 = currentForestCost2;
                                    break; // USE COST MODEL - Insert G_{lG,rG}.
                            }

                            sp1 += (treesSwapped ? _costModel.Insert(lFNode) : _costModel.Delete(lFNode)); // USE COST MODEL - Delete lF, leftmost root node in F_{lF,rF}.
                            // sp1 -- END
                            minCost = sp1; // Start with sp1 as minimal value.
                            // sp2 -- START
                            if (currentForestSize2 == 1)
                            {
                                // G_{lG,rG} is a single node.
                                sp2 = currentForestCost1; // USE COST MODEL - Delete F_{lF,rF}.
                            }
                            else
                            {
                                // G_{lG,rG} is a tree.
                                sp2 = _q[lF];
                            }

                            sp2 += (treesSwapped ? _costModel.Delete(it2Nodes[lG]) : _costModel.Insert(it2Nodes[lG])); // USE COST MODEL - Insert lG, leftmost root node in G_{lG,rG}.
                            if (sp2 < minCost)
                            {
                                // Check if sp2 is minimal value.
                                minCost = sp2;
                            }

                            // sp2 -- END
                            // sp3 -- START
                            if (sp3 < minCost)
                            {
                                sp3 += treesSwapped ? _delta[lG][lF] : sp3deltapointer![lG];
                                if (sp3 < minCost)
                                {
                                    sp3 += (treesSwapped
                                        ? _costModel.Update(it2Nodes[lG], lFNode)
                                        : _costModel.Update(lFNode, it2Nodes[lG])); // USE COST MODEL - Rename the leftmost root nodes in F_{lF,rF} and G_{lG,rG}.
                                    if (sp3 < minCost)
                                    {
                                        minCost = sp3;
                                    }
                                }
                            }

                            // sp3 -- END
                            swritepointer[lG - it2PreLoff] = minCost;
                            // Go to next lG.
                            lG = _ft[lG];
                            _counter++;
                            // Loop D [1, Algorithm 3] - for all nodes to the left of rG.
                            while (lG >= lGlast)
                            {
                                // Increment size and cost of G forest by node lG.
                                currentForestSize2++;
                                currentForestCost2 += (treesSwapped ? _costModel.Delete(it2Nodes[lG]) : _costModel.Insert(it2Nodes[lG]));
                                switch (sp1Source)
                                {
                                    case 1:
                                        sp1 = sp1Spointer[lG - it2PreLoff] + (treesSwapped ? _costModel.Insert(lFNode) : _costModel.Delete(lFNode));
                                        break; // USE COST MODEL - Delete lF, leftmost root node in F_{lF,rF}.
                                    case 2:
                                        sp1 = t[lG - it2PreLoff][rG - it2PreRoff] + (treesSwapped ? _costModel.Insert(lFNode) : _costModel.Delete(lFNode));
                                        break; // USE COST MODEL - Delete lF, leftmost root node in F_{lF,rF}.
                                    case 3:
                                        sp1 = currentForestCost2 + (treesSwapped ? _costModel.Insert(lFNode) : _costModel.Delete(lFNode));
                                        break; // USE COST MODEL - Insert G_{lG,rG} and delete lF, leftmost root node in F_{lF,rF}.
                                }

                                sp2 = sp2Spointer[_fn[lG] - it2PreLoff] +
                                      (treesSwapped ? _costModel.Delete(it2Nodes[lG]) : _costModel.Insert(it2Nodes[lG])); // USE COST MODEL - Insert lG, leftmost root node in G_{lG,rG}.
                                minCost = sp1;
                                if (sp2 < minCost)
                                {
                                    minCost = sp2;
                                }

                                sp3 = treesSwapped ? _delta[lG][lF] : sp3deltapointer![lG];
                                if (sp3 < minCost)
                                {
                                    switch (sp3Source)
                                    {
                                        case 1:
                                            sp3 += sp3Spointer[_fn[(lG + it2Sizes[lG]) - 1] - it2PreLoff];
                                            break;
                                        case 2:
                                            sp3 += currentForestCost2 - (treesSwapped ? it2.PreLToSumDelCost[lG] : it2.PreLToSumInsCost[lG]);
                                            break; // USE COST MODEL - Insert G_{lG,rG}-G_lG.
                                        case 3:
                                            sp3 += t[_fn[(lG + it2Sizes[lG]) - 1] - it2PreLoff][rG - it2PreRoff];
                                            break;
                                    }

                                    if (sp3 < minCost)
                                    {
                                        sp3 += (treesSwapped
                                            ? _costModel.Update(it2Nodes[lG], lFNode)
                                            : _costModel.Update(lFNode, it2Nodes[lG])); // USE COST MODEL - Rename the leftmost root nodes in F_{lF,rF} and G_{lG,rG}.
                                        if (sp3 < minCost)
                                        {
                                            minCost = sp3;
                                        }
                                    }
                                }

                                swritepointer[lG - it2PreLoff] = minCost;
                                lG = _ft[lG];
                                _counter++;
                            }
                        }

                        if (rGminus1InPreL == parentOfRGInPreL)
                        {
                            if (!rightPart)
                            {
                                if (leftPart)
                                {
                                    if (treesSwapped)
                                    {
                                        _delta[parentOfRGInPreL][endPathNode] = s[(lFlast + 1) - it1PreLoff][(rGminus1InPreL + 1) - it2PreLoff];
                                    }
                                    else
                                    {
                                        _delta[endPathNode][parentOfRGInPreL] = s[(lFlast + 1) - it1PreLoff][(rGminus1InPreL + 1) - it2PreLoff];
                                    }
                                }

                                if (endPathNode > 0 && endPathNode == parentOfEndPathNode + 1 && endPathNodeInPreR == parentOfEndPathNodeInPreR + 1)
                                {
                                    if (treesSwapped)
                                    {
                                        _delta[parentOfRGInPreL][parentOfEndPathNode] = s[lFlast - it1PreLoff][(rGminus1InPreL + 1) - it2PreLoff];
                                    }
                                    else
                                    {
                                        _delta[parentOfEndPathNode][parentOfRGInPreL] = s[lFlast - it1PreLoff][(rGminus1InPreL + 1) - it2PreLoff];
                                    }
                                }
                            }

                            for (var lF = lFfirst; lF >= lFlast; lF--)
                            {
                                _q[lF] = s[lF - it1PreLoff][(parentOfRGInPreL + 1) - it2PreLoff];
                            }
                        }

                        // TODO: first pointers can be precomputed
                        for (var lG = lGfirst; lG >= lGlast; lG = _ft[lG])
                        {
                            t[lG - it2PreLoff][rG - it2PreRoff] = s[lFlast - it1PreLoff][lG - it2PreLoff];
                        }
                    }
                }

                // Deal with nodes to the right of the path.
                if (pathType == 0 || pathType == 2 && rightPart || pathType == 2 && !leftPart && !rightPart)
                {
                    if (startPathNode == -1)
                    {
                        lFfirst = endPathNode;
                        rFfirst = it1PreLToPreR[endPathNode];
                    }
                    else
                    {
                        rFfirst = it1PreLToPreR[startPathNode] - 1;
                        lFfirst = endPathNode + 1;
                    }

                    lFlast = endPathNode;
                    lGlast = currentSubtreePreL2;
                    lGfirst = (lGlast + subtreeSize2) - 1;
                    rFlast = it1PreLToPreR[endPathNode];
                    _fn[_fn.Length - 1] = -1;
                    for (var i = currentSubtreePreL2; i < currentSubtreePreL2 + subtreeSize2; i++)
                    {
                        _fn[i] = -1;
                        _ft[i] = -1;
                    }

                    // Store size and cost of the current forest in F.
                    tmpForestSize1 = currentForestSize1;
                    tmpForestCost1 = currentForestCost1;
                    // Loop B' [1, Algorithm 3] - for all nodes in G.
                    for (var lG = lGfirst; lG >= lGlast; lG--)
                    {
                        rGfirst = it2PreLToPreR[lG];
                        UpdateFnArray(it2.PreRToLn[rGfirst], rGfirst, it2PreLToPreR[currentSubtreePreL2]);
                        UpdateFtArray(it2.PreRToLn[rGfirst], rGfirst);
                        var lF = lFfirst;
                        var lGminus1InPreR = lG <= currentSubtreePreL2 ? 0x7fffffff : it2PreLToPreR[lG - 1];
                        var parentOfLG = it2Parents[lG];
                        var parentOfLGInPreR = parentOfLG == -1 ? -1 : it2PreLToPreR[parentOfLG];
                        // Reset size and cost of forest if F.
                        currentForestSize1 = tmpForestSize1;
                        currentForestCost1 = tmpForestCost1;
                        if (pathType == 0)
                        {
                            if (lG == currentSubtreePreL2)
                            {
                                rGlast = rGfirst;
                            }
                            else if (it2.Children[parentOfLG][0] != lG)
                            {
                                rGlast = rGfirst;
                            }
                            else
                            {
                                rGlast = it2PreLToPreR[parentOfLG] + 1;
                            }
                        }
                        else
                        {
                            rGlast = rGfirst == it2PreLToPreR[currentSubtreePreL2] ? rGfirst : it2PreLToPreR[currentSubtreePreL2];
                        }

                        // Loop C' [1, Algorithm 3] - for all nodes to the right of the path node.
                        for (var rF = rFfirst; rF >= rFlast; rF--)
                        {
                            if (rF == rFlast)
                            {
                                lF = lFlast;
                            }

                            var rFInPreL = it1PreRToPreL[rF];
                            // Increment size and cost of F forest by node rF.
                            currentForestSize1++;
                            currentForestCost1 +=
                                (treesSwapped ? _costModel.Insert(it1.PreLToNode[rFInPreL]) : _costModel.Delete(it1.PreLToNode[rFInPreL])); // USE COST MODEL - sum up deletion cost of a forest.
                            // Reset size and cost of G forest to G_lG.
                            currentForestSize2 = it2Sizes[lG];
                            currentForestCost2 = (treesSwapped ? it2.PreLToSumDelCost[lG] : it2.PreLToSumInsCost[lG]); // USE COST MODEL - reset to subtree insertion cost.
                            var rFSubtreeSize = it1Sizes[rFInPreL];
                            if (startPathNode > 0)
                            {
                                rFIsConsecutiveNodeOfCurrentPathNode = startPathNodeInPreR - rF == 1;
                                rFIsRightSiblingOfCurrentPathNode = rF + rFSubtreeSize == startPathNodeInPreR;
                            }
                            else
                            {
                                rFIsConsecutiveNodeOfCurrentPathNode = false;
                                rFIsRightSiblingOfCurrentPathNode = false;
                            }

                            fForestIsTree = rFInPreL == lF;
                            var rFNode = it1.PreLToNode[rFInPreL];
                            sp1Spointer = s[(rF + 1) - it1PreRoff];
                            sp2Spointer = s[rF - it1PreRoff];
                            sp3Spointer = s[0];
                            sp3deltapointer = treesSwapped ? null : _delta[rFInPreL];
                            swritepointer = s[rF - it1PreRoff];
                            var sp1Tpointer = t[lG - it2PreLoff];
                            var sp3Tpointer = t[lG - it2PreLoff];
                            sp1Source = 1;
                            sp3Source = 1;
                            if (fForestIsTree)
                            {
                                if (rFSubtreeSize == 1)
                                {
                                    sp1Source = 3;
                                }
                                else if (rFIsConsecutiveNodeOfCurrentPathNode)
                                {
                                    sp1Source = 2;
                                }

                                sp3 = 0;
                                sp3Source = 2;
                            }
                            else
                            {
                                if (rFIsConsecutiveNodeOfCurrentPathNode)
                                {
                                    sp1Source = 2;
                                }

                                sp3 = currentForestCost1 - (treesSwapped ? it1.PreLToSumInsCost[rFInPreL] : it1.PreLToSumDelCost[rFInPreL]); // USE COST MODEL - Delete F_{lF,rF}-F_rF.
                                if (rFIsRightSiblingOfCurrentPathNode)
                                {
                                    sp3Source = 3;
                                }
                            }

                            if (sp3Source == 1)
                            {
                                sp3Spointer = s[(rF + rFSubtreeSize) - it1PreRoff];
                            }

                            if (currentForestSize2 == 1)
                            {
                                sp2 = currentForestCost1; // USE COST MODEL - Delete F_{lF,rF}.
                            }
                            else
                            {
                                sp2 = _q[rF];
                            }

                            var rG = rGfirst;
                            var rGfirstInPreL = it2PreRToPreL[rGfirst];
                            currentForestSize2++;
                            switch (sp1Source)
                            {
                                case 1:
                                    sp1 = sp1Spointer[rG - it2PreRoff];
                                    break;
                                case 2:
                                    sp1 = sp1Tpointer[rG - it2PreRoff];
                                    break;
                                case 3:
                                    sp1 = currentForestCost2;
                                    break; // USE COST MODEL - Insert G_{lG,rG}.
                            }

                            sp1 += (treesSwapped ? _costModel.Insert(rFNode) : _costModel.Delete(rFNode)); // USE COST MODEL - Delete rF.
                            minCost = sp1;
                            sp2 += (treesSwapped ? _costModel.Delete(it2Nodes[rGfirstInPreL]) : _costModel.Insert(it2Nodes[rGfirstInPreL])); // USE COST MODEL - Insert rG.
                            if (sp2 < minCost)
                            {
                                minCost = sp2;
                            }

                            if (sp3 < minCost)
                            {
                                sp3 += treesSwapped ? _delta[rGfirstInPreL][rFInPreL] : sp3deltapointer![rGfirstInPreL];
                                if (sp3 < minCost)
                                {
                                    sp3 += (treesSwapped ? _costModel.Update(it2Nodes[rGfirstInPreL], rFNode) : _costModel.Update(rFNode, it2Nodes[rGfirstInPreL]));
                                    if (sp3 < minCost)
                                    {
                                        minCost = sp3;
                                    }
                                }
                            }

                            swritepointer[rG - it2PreRoff] = minCost;
                            rG = _ft[rG];
                            _counter++;
                            // Loop D' [1, Algorithm 3] - for all nodes to the right of lG;
                            while (rG >= rGlast)
                            {
                                rGInPreL = it2PreRToPreL[rG];
                                // Increment size and cost of G forest by node rG.
                                currentForestSize2++;
                                currentForestCost2 += (treesSwapped ? _costModel.Delete(it2Nodes[rGInPreL]) : _costModel.Insert(it2Nodes[rGInPreL]));
                                switch (sp1Source)
                                {
                                    case 1:
                                        sp1 = sp1Spointer[rG - it2PreRoff] + (treesSwapped ? _costModel.Insert(rFNode) : _costModel.Delete(rFNode));
                                        break; // USE COST MODEL - Delete rF.
                                    case 2:
                                        sp1 = sp1Tpointer[rG - it2PreRoff] + (treesSwapped ? _costModel.Insert(rFNode) : _costModel.Delete(rFNode));
                                        break; // USE COST MODEL - Delete rF.
                                    case 3:
                                        sp1 = currentForestCost2 + (treesSwapped ? _costModel.Insert(rFNode) : _costModel.Delete(rFNode));
                                        break; // USE COST MODEL - Insert G_{lG,rG} and delete rF.
                                }

                                sp2 = sp2Spointer[_fn[rG] - it2PreRoff] + (treesSwapped ? _costModel.Delete(it2Nodes[rGInPreL]) : _costModel.Insert(it2Nodes[rGInPreL])); // USE COST MODEL - Insert rG.
                                minCost = sp1;
                                if (sp2 < minCost)
                                {
                                    minCost = sp2;
                                }

                                sp3 = treesSwapped ? _delta[rGInPreL][rFInPreL] : sp3deltapointer![rGInPreL];
                                if (sp3 < minCost)
                                {
                                    switch (sp3Source)
                                    {
                                        case 1:
                                            sp3 += sp3Spointer[_fn[(rG + it2Sizes[rGInPreL]) - 1] - it2PreRoff];
                                            break;
                                        case 2:
                                            sp3 += currentForestCost2 - (treesSwapped ? it2.PreLToSumDelCost[rGInPreL] : it2.PreLToSumInsCost[rGInPreL]);
                                            break; // USE COST MODEL - Insert G_{lG,rG}-G_rG.
                                        case 3:
                                            sp3 += sp3Tpointer[_fn[(rG + it2Sizes[rGInPreL]) - 1] - it2PreRoff];
                                            break;
                                    }

                                    if (sp3 < minCost)
                                    {
                                        sp3 += (treesSwapped ? _costModel.Update(it2Nodes[rGInPreL], rFNode) : _costModel.Update(rFNode, it2Nodes[rGInPreL])); // USE COST MODEL - Rename rF to rG.
                                        if (sp3 < minCost)
                                        {
                                            minCost = sp3;
                                        }
                                    }
                                }

                                swritepointer[rG - it2PreRoff] = minCost;
                                rG = _ft[rG];
                                _counter++;
                            }
                        }

                        if (lG > currentSubtreePreL2 && lG - 1 == parentOfLG)
                        {
                            if (rightPart)
                            {
                                if (treesSwapped)
                                {
                                    _delta[parentOfLG][endPathNode] = s[(rFlast + 1) - it1PreRoff][(lGminus1InPreR + 1) - it2PreRoff];
                                }
                                else
                                {
                                    _delta[endPathNode][parentOfLG] = s[(rFlast + 1) - it1PreRoff][(lGminus1InPreR + 1) - it2PreRoff];
                                }
                            }

                            if (endPathNode > 0 && endPathNode == parentOfEndPathNode + 1 && endPathNodeInPreR == parentOfEndPathNodeInPreR + 1)
                                if (treesSwapped)
                                {
                                    _delta[parentOfLG][parentOfEndPathNode] = s[rFlast - it1PreRoff][(lGminus1InPreR + 1) - it2PreRoff];
                                }
                                else
                                {
                                    _delta[parentOfEndPathNode][parentOfLG] = s[rFlast - it1PreRoff][(lGminus1InPreR + 1) - it2PreRoff];
                                }

                            for (var rF = rFfirst; rF >= rFlast; rF--)
                            {
                                _q[rF] = s[rF - it1PreRoff][(parentOfLGInPreR + 1) - it2PreRoff];
                            }
                        }

                        // TODO: first pointers can be precomputed
                        for (var rG = rGfirst; rG >= rGlast; rG = _ft[rG])
                        {
                            t[lG - it2PreLoff][rG - it2PreRoff] = s[rFlast - it1PreRoff][rG - it2PreRoff];
                        }
                    }
                }

                // Walk up the path by one node.
                startPathNode = endPathNode;
                endPathNode = it1Parents[endPathNode];
            }

            return minCost;
        }

        // ===================== BEGIN spfL
    
        /// <summary>
        /// Implements single-path function for left paths [1, Sections 3.3,3.4,3.5].
        /// The parameters represent input subtrees for the single-path function.
        /// The order of the parameters is important. We use this single-path function
        /// due to better performance compared to spfA.
        /// </summary>
        /// <param name="it1">node indexer of the left-hand input subtree.</param>
        /// <param name="it2">node indexer of the right-hand input subtree.</param>
        /// <param name="treesSwapped">says if the order of input subtrees has been swapped compared to the order of the initial input trees. Used for accessing delta array and deciding on the edit operation.</param>
        /// <returns>tree edit distance between left-hand and right-hand input subtrees.</returns>
        private float SpfL(NodeIndexer<T, TCostModel> it1, NodeIndexer<T, TCostModel> it2, bool treesSwapped)
        {
            // Initialise the array to store the keyroot nodes in the right-hand input
            // subtree.
            var keyRoots = new int[it2.Sizes[it2.GetCurrentNode()]];
            Array.Fill(keyRoots, -1);
            // Get the leftmost leaf node of the right-hand input subtree.
            var pathId = it2.preL_to_lld(it2.GetCurrentNode());
            // Calculate the keyroot nodes in the right-hand input subtree.
            // firstKeyRoot is the index in keyRoots of the first keyroot node that
            // we have to process. We need this index because keyRoots array is larger
            // than the number of keyroot nodes.
            var firstKeyRoot = ComputeKeyRoots(it2, it2.GetCurrentNode(), pathId, keyRoots, 0);
            // Initialise an array to store intermediate distances for subforest pairs.
            float[][] forestdist = new float[it1.Sizes[it1.GetCurrentNode()] + 1][];
            for (var i = 0; i < forestdist.Length; i++)
                forestdist[i] = new float[it2.Sizes[it2.GetCurrentNode()] + 1];

            // Compute the distances between pairs of keyroot nodes. In the left-hand
            // input subtree only the root is the keyroot. Thus, we compute the distance
            // between the left-hand input subtree and all keyroot nodes in the
            // right-hand input subtree.
            for (var i = firstKeyRoot - 1; i >= 0; i--)
            {
                TreeEditDist(it1, it2, it1.GetCurrentNode(), keyRoots[i], forestdist, treesSwapped);
            }

            // Return the distance between the input subtrees.
            return forestdist[it1.Sizes[it1.GetCurrentNode()]][it2.Sizes[it2.GetCurrentNode()]];
        }

        /// <summary>
        /// Calculates and stores keyroot nodes for left paths of the given subtree
        /// recursively.
        /// </summary>
        /// <param name="it2"> node indexer.</param>
        /// <param name="subtreeRootNode"> keyroot node - recursion point.</param>
        /// <param name="pathId"> left-to-right preorder id of the leftmost leaf node of subtreeRootNode.</param>
        /// <param name="keyRoots"> array that stores all key roots in the order of their left-to-right preorder ids.</param>
        /// <param name="index"> the index of keyRoots array where to store the next keyroot node.</param>
        /// <returns>the index of the first keyroot node to process.</returns>
        // TODO: Merge with computeRevKeyRoots - the only difference is between leftmost and rightmost leaf.
        private int ComputeKeyRoots(NodeIndexer<T, TCostModel> it2, int subtreeRootNode, int pathId, int[] keyRoots, int index)
        {
            // The subtreeRootNode is a keyroot node. Add it to keyRoots.
            keyRoots[index] = subtreeRootNode;
            // Increment the index to know where to store the next keyroot node.
            index++;
            // Walk up the left path starting with the leftmost leaf of subtreeRootNode,
            // until the child of subtreeRootNode.
            var pathNode = pathId;
            while (pathNode > subtreeRootNode)
            {
                var parent = it2.Parents[pathNode];
                // For each sibling to the right of pathNode, execute this method recursively.
                // Each right sibling of pathNode is a keyroot node.
                foreach (var child in it2.Children[parent])
                {
                    // Execute computeKeyRoots recursively for the new subtree rooted at child and child's leftmost leaf node.
                    if (child != pathNode) index = ComputeKeyRoots(it2, child, it2.preL_to_lld(child), keyRoots, index);
                }

                // Walk up.
                pathNode = parent;
            }

            return index;
        }

        /// <summary>
        /// Implements the core of spfL. Fills in forestdist array with intermediate
        /// distances of sub-forest pairs in dynamic-programming fashion.
        /// </summary>
        /// <param name="it1"> node indexer of the left-hand input subtree.</param>
        /// <param name="it2"> node indexer of the right-hand input subtree.</param>
        /// <param name="it1Subtree"> left-to-right preorder id of the root node of the left-hand input subtree.</param>
        /// <param name="it2Subtree"> left-to-right preorder id of the root node of the right-hand input subtree.</param>
        /// <param name="forestdist"> the array to be filled in with intermediate distances of sub-forest pairs.</param>
        /// <param name="treesSwapped"> says if the order of input subtrees has been swapped compared to the order of the initial input trees. Used for accessing delta array and deciding on the edit operation.</param>
        private void TreeEditDist(NodeIndexer<T, TCostModel> it1, NodeIndexer<T, TCostModel> it2, int it1Subtree, int it2Subtree, float[][] forestdist, bool treesSwapped)
        {
            // Translate input subtree root nodes to left-to-right postorder.
            var i = it1.PreLToPostL[it1Subtree];
            var j = it2.PreLToPostL[it2Subtree];
            // We need to offset the node ids for accessing forestdist array which has
            // indices from 0 to subtree size. However, the subtree node indices do not
            // necessarily start with 0.
            // Whenever the original left-to-right postorder id has to be accessed, use
            // i+ioff and j+joff.
            var ioff = it1.PostLToLld[i] - 1;
            var joff = it2.PostLToLld[j] - 1;
            // Variables holding costs of each minimum element.
            float dc = 0;
            // Initialize forestdist array with deletion and insertion costs of each
            // relevant subforest.
            forestdist[0][0] = 0;
            for (var i1 = 1; i1 <= i - ioff; i1++)
            {
                forestdist[i1][0] = forestdist[i1 - 1][0] +
                                    (treesSwapped ? _costModel.Insert(it1.postL_to_node(i1 + ioff)) : _costModel.Delete(it1.postL_to_node(i1 + ioff))); // USE COST MODEL - delete i1.
            }

            for (var j1 = 1; j1 <= j - joff; j1++)
            {
                forestdist[0][j1] = forestdist[0][j1 - 1] +
                                    (treesSwapped ? _costModel.Delete(it2.postL_to_node(j1 + joff)) : _costModel.Insert(it2.postL_to_node(j1 + joff))); // USE COST MODEL - insert j1.
            }

            // Fill in the remaining costs.
            for (var i1 = 1; i1 <= i - ioff; i1++)
            {
                for (var j1 = 1; j1 <= j - joff; j1++)
                {
                    // Increment the number of sub-problems.
                    _counter++;
                    // Calculate partial distance values for this sub-problem.
                    var u = (treesSwapped
                        ? _costModel.Update(it2.postL_to_node(j1 + joff), it1.postL_to_node(i1 + ioff))
                        : _costModel.Update(it1.postL_to_node(i1 + ioff), it2.postL_to_node(j1 + joff))); // USE COST MODEL - update i1 to j1.
                    var da = forestdist[i1 - 1][j1] + (treesSwapped ? _costModel.Insert(it1.postL_to_node(i1 + ioff)) : _costModel.Delete(it1.postL_to_node(i1 + ioff))); // USE COST MODEL - delete i1.
                    var db = forestdist[i1][j1 - 1] + (treesSwapped ? _costModel.Delete(it2.postL_to_node(j1 + joff)) : _costModel.Insert(it2.postL_to_node(j1 + joff))); // USE COST MODEL - insert j1.
                    // If current subforests are subtrees.
                    if (it1.PostLToLld[i1 + ioff] == it1.PostLToLld[i] && it2.PostLToLld[j1 + joff] == it2.PostLToLld[j])
                    {
                        dc = forestdist[i1 - 1][j1 - 1] + u;
                        // Store the relevant distance value in delta array.
                        if (treesSwapped)
                        {
                            _delta[it2.PostLToPreL[j1 + joff]][it1.PostLToPreL[i1 + ioff]] = forestdist[i1 - 1][j1 - 1];
                        }
                        else
                        {
                            _delta[it1.PostLToPreL[i1 + ioff]][it2.PostLToPreL[j1 + joff]] = forestdist[i1 - 1][j1 - 1];
                        }
                    }
                    else
                    {
                        dc = forestdist[it1.PostLToLld[i1 + ioff] - 1 - ioff][it2.PostLToLld[j1 + joff] - 1 - joff] +
                             (treesSwapped ? _delta[it2.PostLToPreL[j1 + joff]][it1.PostLToPreL[i1 + ioff]] : _delta[it1.PostLToPreL[i1 + ioff]][it2.PostLToPreL[j1 + joff]]) + u;
                    }

                    // Calculate final minimum.
                    forestdist[i1][j1] = da >= db ? db >= dc ? dc : db : da >= dc ? dc : da;
                }
            }
        }
        // ===================== END spfL

        // ===================== BEGIN spfR
        /// <summary>
        /// Implements single-path function for right paths [1, Sections 3.3,3.4,3.5].
        /// The parameters represent input subtrees for the single-path function.
        /// The order of the parameters is important. We use this single-path function
        /// due to better performance compared to spfA.
        /// </summary>
        /// <param name="it1"> node indexer of the left-hand input subtree.</param>
        /// <param name="it2"> node indexer of the right-hand input subtree.</param>
        /// <param name="treesSwapped"> says if the order of input subtrees has been swapped compared to the order of the initial input trees. Used for accessing delta array and deciding on the edit operation.</param>
        /// <returns>tree edit distance between left-hand and right-hand input subtrees.</returns>
        private float SpfR(NodeIndexer<T, TCostModel> it1, NodeIndexer<T, TCostModel> it2, bool treesSwapped)
        {
            // Initialise the array to store the keyroot nodes in the right-hand input
            // subtree.
            var revKeyRoots = new int[it2.Sizes[it2.GetCurrentNode()]];
            Array.Fill(revKeyRoots, -1);
            // Get the rightmost leaf node of the right-hand input subtree.
            var pathId = it2.preL_to_rld(it2.GetCurrentNode());
            // Calculate the keyroot nodes in the right-hand input subtree.
            // firstKeyRoot is the index in keyRoots of the first keyroot node that
            // we have to process. We need this index because keyRoots array is larger
            // than the number of keyroot nodes.
            var firstKeyRoot = ComputeRevKeyRoots(it2, it2.GetCurrentNode(), pathId, revKeyRoots, 0);
            // Initialise an array to store intermediate distances for sub-forest pairs.
            float[][] forestdist = new float[it1.Sizes[it1.GetCurrentNode()] + 1][];
            for (var i = 0; i < forestdist.Length; i++)
                forestdist[i] = new float[it2.Sizes[it2.GetCurrentNode()] + 1];
            // Compute the distances between pairs of keyroot nodes. In the left-hand
            // input subtree only the root is the keyroot. Thus, we compute the distance
            // between the left-hand input subtree and all keyroot nodes in the
            // right-hand input subtree.
            for (var i = firstKeyRoot - 1; i >= 0; i--)
            {
                RevTreeEditDist(it1, it2, it1.GetCurrentNode(), revKeyRoots[i], forestdist, treesSwapped);
            }

            // Return the distance between the input subtrees.
            return forestdist[it1.Sizes[it1.GetCurrentNode()]][it2.Sizes[it2.GetCurrentNode()]];
        }

        /// <summary>
        /// Calculates and stores keyroot nodes for right paths of the given subtree
        /// recursively.
        /// </summary>
        /// <param name="it2"> node indexer.</param>
        /// <param name="subtreeRootNode"> keyroot node - recursion point.</param>
        /// <param name="pathId"> left-to-right preorder id of the rightmost leaf node of subtreeRootNode.</param>
        /// <param name="revKeyRoots"> array that stores all key roots in the order of their left-to-right preorder ids.</param>
        /// <param name="index"> the index of keyRoots array where to store the next keyroot node.</param>
        /// <returns>the index of the first keyroot node to process.</returns>
        private int ComputeRevKeyRoots(NodeIndexer<T, TCostModel> it2, int subtreeRootNode, int pathId, int[] revKeyRoots, int index)
        {
            // The subtreeRootNode is a keyroot node. Add it to keyRoots.
            revKeyRoots[index] = subtreeRootNode;
            // Increment the index to know where to store the next keyroot node.
            index++;
            // Walk up the right path starting with the rightmost leaf of
            // subtreeRootNode, until the child of subtreeRootNode.
            var pathNode = pathId;
            while (pathNode > subtreeRootNode)
            {
                var parent = it2.Parents[pathNode];
                // For each sibling to the left of pathNode, execute this method recursively.
                // Each left sibling of pathNode is a keyroot node.
                foreach (var child in it2.Children[parent])
                {
                    // Execute computeRevKeyRoots recursively for the new subtree rooted at child and child's rightmost leaf node.
                    if (child != pathNode) index = ComputeRevKeyRoots(it2, child, it2.preL_to_rld(child), revKeyRoots, index);
                }

                // Walk up.
                pathNode = parent;
            }

            return index;
        }

        /// <summary>
        /// Implements the core of spfR. Fills in forestdist array with intermediate
        /// distances of sub-forest pairs in dynamic-programming fashion.
        /// </summary>
        /// <param name="it1"> node indexer of the left-hand input subtree.</param>
        /// <param name="it2"> node indexer of the right-hand input subtree.</param>
        /// <param name="it1Subtree"> left-to-right preorder id of the root node of the left-hand input subtree.</param>
        /// <param name="it2Subtree"> left-to-right preorder id of the root node of the right-hand input subtree.</param>
        /// <param name="forestdist"> the array to be filled in with intermediate distances of sub-forest pairs.</param>
        /// <param name="treesSwapped"> says if the order of input subtrees has been swapped compared to the order of the initial input trees. Used for accessing delta array and deciding on the edit operation.</param>
        private void RevTreeEditDist(NodeIndexer<T, TCostModel> it1, NodeIndexer<T, TCostModel> it2, int it1Subtree, int it2Subtree, float[][] forestdist, bool treesSwapped)
        {
            // Translate input subtree root nodes to right-to-left postorder.
            var i = it1.PreLToPostR[it1Subtree];
            var j = it2.PreLToPostR[it2Subtree];
            // We need to offset the node ids for accessing forestdist array which has
            // indices from 0 to subtree size. However, the subtree node indices do not
            // necessarily start with 0.
            // Whenever the original right-to-left postorder id has to be accessed, use
            // i+ioff and j+joff.
            var ioff = it1.PostRToRld[i] - 1;
            var joff = it2.PostRToRld[j] - 1;
            // Variables holding costs of each minimum element.
            float dc = 0;
            // Initialize forestdist array with deletion and insertion costs of each
            // relevant subforest.
            forestdist[0][0] = 0;
            for (var i1 = 1; i1 <= i - ioff; i1++)
            {
                forestdist[i1][0] = forestdist[i1 - 1][0] +
                                    (treesSwapped ? _costModel.Insert(it1.postR_to_node(i1 + ioff)) : _costModel.Delete(it1.postR_to_node(i1 + ioff))); // USE COST MODEL - delete i1.
            }

            for (var j1 = 1; j1 <= j - joff; j1++)
            {
                forestdist[0][j1] = forestdist[0][j1 - 1] +
                                    (treesSwapped ? _costModel.Delete(it2.postR_to_node(j1 + joff)) : _costModel.Insert(it2.postR_to_node(j1 + joff))); // USE COST MODEL - insert j1.
            }

            // Fill in the remaining costs.
            for (var i1 = 1; i1 <= i - ioff; i1++)
            {
                for (var j1 = 1; j1 <= j - joff; j1++)
                {
                    // Increment the number of sub-problems.
                    _counter++;
                    // Calculate partial distance values for this sub-problem.
                    var u = (treesSwapped
                        ? _costModel.Update(it2.postR_to_node(j1 + joff), it1.postR_to_node(i1 + ioff))
                        : _costModel.Update(it1.postR_to_node(i1 + ioff), it2.postR_to_node(j1 + joff))); // USE COST MODEL - update i1 to j1.
                    var da = forestdist[i1 - 1][j1] + (treesSwapped ? _costModel.Insert(it1.postR_to_node(i1 + ioff)) : _costModel.Delete(it1.postR_to_node(i1 + ioff))); // USE COST MODEL - delete i1.
                    var db = forestdist[i1][j1 - 1] + (treesSwapped ? _costModel.Delete(it2.postR_to_node(j1 + joff)) : _costModel.Insert(it2.postR_to_node(j1 + joff))); // USE COST MODEL - insert j1.
                    // If current subforests are subtrees.
                    if (it1.PostRToRld[i1 + ioff] == it1.PostRToRld[i] && it2.PostRToRld[j1 + joff] == it2.PostRToRld[j])
                    {
                        dc = forestdist[i1 - 1][j1 - 1] + u;
                        // Store the relevant distance value in delta array.
                        if (treesSwapped)
                        {
                            _delta[it2.PostRToPreL[j1 + joff]][it1.PostRToPreL[i1 + ioff]] = forestdist[i1 - 1][j1 - 1];
                        }
                        else
                        {
                            _delta[it1.PostRToPreL[i1 + ioff]][it2.PostRToPreL[j1 + joff]] = forestdist[i1 - 1][j1 - 1];
                        }
                    }
                    else
                    {
                        dc = forestdist[it1.PostRToRld[i1 + ioff] - 1 - ioff][it2.PostRToRld[j1 + joff] - 1 - joff] +
                             (treesSwapped ? _delta[it2.PostRToPreL[j1 + joff]][it1.PostRToPreL[i1 + ioff]] : _delta[it1.PostRToPreL[i1 + ioff]][it2.PostRToPreL[j1 + joff]]) + u;
                    }

                    // Calculate final minimum.
                    forestdist[i1][j1] = da >= db ? db >= dc ? dc : db : da >= dc ? dc : da;
                }
            }
        }
        // ===================== END spfR

        /// <summary>
        /// Decodes the path from the optimal strategy to its type.
        /// </summary>
        /// <param name="pathIdWithPathIdOffset"> raw path id from strategy array.</param>
        /// <param name="pathIdOffset"> offset used to distinguish between paths in the source and destination trees.</param>
        /// <param name="it"> node indexer.</param>
        /// <param name="currentRootNodePreL"> the left-to-right preorder id of the current subtree processed in tree decomposition phase.</param>
        /// <param name="currentSubtreeSize"> the size of the subtree currently processed in tree decomposition phase.</param>
        /// <returns>type of the strategy path (LEFT, RIGHT, INNER).</returns>
        private byte GetStrategyPathType(int pathIdWithPathIdOffset, int pathIdOffset, NodeIndexer<T, TCostModel> it, int currentRootNodePreL, int currentSubtreeSize)
        {
            if (Math.Sign(pathIdWithPathIdOffset) == -1)
            {
                return Left;
            }

            var pathId = Math.Abs(pathIdWithPathIdOffset) - 1;
            if (pathId >= pathIdOffset)
            {
                pathId = pathId - pathIdOffset;
            }

            if (pathId == (currentRootNodePreL + currentSubtreeSize) - 1)
            {
                return Right;
            }

            return Inner;
        }

        /// <summary>
        /// fn array used in the algorithm before [1]. Using it does not change the
        /// complexity.
        /// </summary>
        /// <param name="lnForNode">---</param>
        /// <param name="node">---</param>
        /// <param name="currentSubtreePreL">---</param>
        // TODO: Do not use it [1, Section 8.4].
        private void UpdateFnArray(int lnForNode, int node, int currentSubtreePreL)
        {
            if (lnForNode >= currentSubtreePreL)
            {
                _fn[node] = _fn[lnForNode];
                _fn[lnForNode] = node;
            }
            else
            {
                _fn[node] = _fn[_fn.Length - 1];
                _fn[_fn.Length - 1] = node;
            }
        }

        /// <summary>
        /// ft array used in the algorithm before [1]. Using it does not change the
        /// complexity.
        /// </summary>
        /// <param name="lnForNode"> ---</param>
        /// <param name="node"> ---</param>
        // TODO: Do not use it [1, Section 8.4].
        private void UpdateFtArray(int lnForNode, int node)
        {
            _ft[node] = lnForNode;
            if (_fn[node] > -1)
            {
                _ft[_fn[node]] = node;
            }
        }

        /// <summary>
        /// Compute the edit mapping between two trees. The trees are input trees
        /// to the distance computation and the distance must be computed before
        /// computing the edit mapping (distances of subtree pairs are required).
        /// </summary>
        /// <returns>
        /// Returns list of pairs of nodes that are mapped as pairs of their
        /// postorder IDs (starting with 1). Nodes that are deleted or
        /// inserted are mapped to 0.
        /// </returns>
        // TODO: Mapping computation requires more thorough documentation
        //       (methods computeEditMapping, forestDist, mappingCost).
        // TODO: Before computing the mapping, verify if TED has been computed.
        //       Mapping computation should trigger distance computation if
        //       necessary.
        public List<int[]> ComputeEditMapping()
        {
            // Initialize tree and forest distance arrays.
            // Arrays for subtree distances is not needed because the distances
            // between subtrees without the root nodes are already stored in delta.
            var forestdist = new float[_size1 + 1][];
            for (var i = 0; i < forestdist.Length; i++)
                forestdist[i] = new float[_size2 + 1];

            var rootNodePair = true;

            // forestdist for input trees has to be computed
            ForestDist(_it1, _it2, _size1, _size2, forestdist);

            // empty edit mapping
            Stack<int[]> editMapping = new();

            // empty stack of tree Pairs
            Stack<int[]> treePairs = new();

            // push the pair of trees (ted1,ted2) to stack
            treePairs.Push(new[] { _size1, _size2 });

            while (treePairs.Count > 0) // Not empty
            {
                // get next tree pair to be processed
                var treePair = treePairs.Pop();
                var lastRow = treePair[0];
                var lastCol = treePair[1];

                // compute forest distance matrix
                if (!rootNodePair)
                {
                    ForestDist(_it1, _it2, lastRow, lastCol, forestdist);
                }

                rootNodePair = false;

                // compute mapping for current forest distance matrix
                var firstRow = _it1.PostLToLld[lastRow - 1];
                var firstCol = _it2.PostLToLld[lastCol - 1];
                var row = lastRow;
                var col = lastCol;
                while ((row > firstRow) || (col > firstCol))
                {
                    if ((row > firstRow) && (forestdist[row - 1][col] + _costModel.Delete(_it1.postL_to_node(row - 1)) == forestdist[row][col]))
                    {
                        // USE COST MODEL - Delete node row of source tree.
                        // node with postorderID row is deleted from ted1
                        editMapping.Push(new[] { row, 0 });
                        row--;
                    }
                    else if ((col > firstCol) && (forestdist[row][col - 1] + _costModel.Insert(_it2.postL_to_node(col - 1)) == forestdist[row][col]))
                    {
                        // USE COST MODEL - Insert node col of destination tree.
                        // node with postorderID col is inserted into ted2
                        editMapping.Push(new[] { 0, col });
                        col--;
                    }
                    else
                    {
                        // node with postorderID row in ted1 is renamed to node col
                        // in ted2
                        if ((_it1.PostLToLld[row - 1] == _it1.PostLToLld[lastRow - 1]) && (_it2.PostLToLld[col - 1] == _it2.PostLToLld[lastCol - 1]))
                        {
                            // if both sub-forests are trees, map nodes
                            editMapping.Push(new[] { row, col });
                            row--;
                            col--;
                        }
                        else
                        {
                            // push subtree pair
                            treePairs.Push(new[] { row, col });

                            // continue with forest to the left of the popped
                            // subtree pair
                            row = _it1.PostLToLld[row - 1];
                            col = _it2.PostLToLld[col - 1];
                        }
                    }
                }
            }

            return editMapping.ToList();
        }


        /// <summary>
        /// Recalculates distances between sub-forests of two subtrees. These values
        /// are used in mapping computation to track back the origin of minimum values.
        /// It is based on Zhang and Shasha algorithm.
        /// 
        /// The rename cost must be added in the last line. Otherwise the formula is
        /// incorrect. This is due to delta storing distances between subtrees
        /// without the root nodes.
        /// 
        /// i and j are postorder ids of the nodes - starting with 1.
        /// </summary>
        /// <param name="ted1"> node indexer of the source input tree.</param>
        /// <param name="ted2"> node indexer of the destination input tree.</param>
        /// <param name="i"> subtree root of source tree that is to be mapped.</param>
        /// <param name="j"> subtree root of destination tree that is to be mapped.</param>
        /// <param name="forestDist"> array to store distances between sub-forest pairs.</param>
        private void ForestDist(NodeIndexer<T, TCostModel> ted1, NodeIndexer<T, TCostModel> ted2, int i, int j, float[][] forestDist)
        {
            forestDist[ted1.PostLToLld[i - 1]][ted2.PostLToLld[j - 1]] = 0;

            for (var di = ted1.PostLToLld[i - 1] + 1; di <= i; di++)
            {
                forestDist[di][ted2.PostLToLld[j - 1]] = forestDist[di - 1][ted2.PostLToLld[j - 1]] + _costModel.Delete(ted1.postL_to_node(di - 1));
                for (var dj = ted2.PostLToLld[j - 1] + 1; dj <= j; dj++)
                {
                    forestDist[ted1.PostLToLld[i - 1]][dj] = forestDist[ted1.PostLToLld[i - 1]][dj - 1] + _costModel.Insert(ted2.postL_to_node(dj - 1));
                    var costRen = _costModel.Update(ted1.postL_to_node(di - 1), ted2.postL_to_node(dj - 1));
                    // TODO: The first two elements of the minimum can be computed here,
                    //       similarly to spfL and spfR.
                    if ((ted1.PostLToLld[di - 1] == ted1.PostLToLld[i - 1]) && (ted2.PostLToLld[dj - 1] == ted2.PostLToLld[j - 1]))
                    {
                        forestDist[di][dj] = Math.Min(Math.Min(
                                forestDist[di - 1][dj] + _costModel.Delete(ted1.postL_to_node(di - 1)),
                                forestDist[di][dj - 1] + _costModel.Insert(ted2.postL_to_node(dj - 1))),
                            forestDist[di - 1][dj - 1] + costRen);
                        // If substituted with delta, this will overwrite the value
                        // in delta.
                        // It looks that we don't have to write this value.
                        // Conceptually it is correct because we already have all
                        // the values in delta for subtrees without the root nodes,
                        // and we need these.
                        // treedist[di][dj] = forestdist[di][dj];
                    }
                    else
                    {
                        // di and dj are postorder ids of the nodes - starting with 1
                        // Substituted 'treedist[di][dj]' with 'delta[it1.postL_to_preL[di-1]][it2.postL_to_preL[dj-1]]'
                        forestDist[di][dj] = Math.Min(Math.Min(
                                forestDist[di - 1][dj] + _costModel.Delete(ted1.postL_to_node(di - 1)),
                                forestDist[di][dj - 1] + _costModel.Insert(ted2.postL_to_node(dj - 1))),
                            forestDist[ted1.PostLToLld[di - 1]][ted2.PostLToLld[dj - 1]] + _delta[_it1.PostLToPreL[di - 1]][_it2.PostLToPreL[dj - 1]] + costRen);
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the cost of an edit mapping. It traverses the mapping and sums
        /// up the cost of each operation. The costs are taken from the cost model.
        /// </summary>
        /// <param name="mapping">an edit mapping.</param>
        /// <returns>cost of edit mapping.</returns>
        public float MappingCost(List<int[]> mapping)
        {
            var cost = 0.0f;
            for (var i = 0; i < mapping.Count; i++)
            {
                if (mapping[i][0] == 0)
                {
                    // Insertion.
                    cost += _costModel.Insert(_it2.postL_to_node(mapping[i][1] - 1));
                }
                else if (mapping[i][1] == 0)
                {
                    // Deletion.
                    cost += _costModel.Delete(_it1.postL_to_node(mapping[i][0] - 1));
                }
                else
                {
                    // Update.
                    cost += _costModel.Update(_it1.postL_to_node(mapping[i][0] - 1), _it2.postL_to_node(mapping[i][1] - 1));
                }
            }

            return cost;
        }
    
        public void ExecuteOperations(
            List<int[]> mapping, 
            IOperationExecutor<T> executor)
        {
            for (var i = 0; i < mapping.Count; i++)
            {
                if (mapping[i][0] == 0)
                {
                    // Insertion.
                    executor.Insert(_it2.postL_to_node(mapping[i][1] - 1));
                }
                else if (mapping[i][1] == 0)
                {
                    // Deletion.
                    executor.Delete(_it1.postL_to_node(mapping[i][0] - 1));
                }
                else
                {
                    // Update.
                    executor.Update(_it1.postL_to_node(mapping[i][0] - 1), _it2.postL_to_node(mapping[i][1] - 1));
                }
            }
        }
    
        public void ExecuteOperationsInReverse(
            List<int[]> mapping, 
            IOperationExecutor<T> executor)
        {
            for (var i = mapping.Count - 1; i >= 0; i--)
            {
                if (mapping[i][0] == 0)
                {
                    // Insertion.
                    executor.Insert(_it2.postL_to_node(mapping[i][1] - 1));
                }
                else if (mapping[i][1] == 0)
                {
                    // Deletion.
                    executor.Delete(_it1.postL_to_node(mapping[i][0] - 1));
                }
                else
                {
                    // Update.
                    executor.Update(_it1.postL_to_node(mapping[i][0] - 1), _it2.postL_to_node(mapping[i][1] - 1));
                }
            }
        }
    }
}