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

using System.Collections.Generic;

namespace AptedSharp
{
    /// <summary>
    ///  Indexes nodes of the input tree to the algorithm that is already parsed to
    /// tree structure using {@link node.Node} class. Stores various indices on
    /// nodes required for efficient computation of APTED [1,2]. Additionally, it
    /// stores single-value properties of the tree.
    ///
    /// For indexing we use four tree traversals that assign ids to the nodes:
    /// * left-to-right preorder [1],
    /// * right-to-left preorder [1],
    /// * left-to-right postorder [2],
    /// * right-to-left postorder [2].
    ///
    /// See the source code for more algorithm-related comments.
    ///
    /// References:
    ///
    /// [1] M. Pawlik and N. Augsten. Efficient Computation of the Tree Edit
    /// Distance. ACM Transactions on Database Systems (TODS) 40(1). 2015.
    /// [2] M. Pawlik and N. Augsten. Tree edit distance: Robust and memory-
    /// efficient. Information Systems 56. 2016.
    /// </summary>
    /// <typeparam name="T">The type of the node data</typeparam>
    /// <typeparam name="TCostModel">The type of the cost model</typeparam>
    public class NodeIndexer<T, TCostModel> where TCostModel : ICostModel<T>
    {
        /*
         * Structure indices.
         */

        /// <summary>
        /// Index from left-to-right preorder id of node n (starting with {@code 0})
        /// to Node object corresponding to n. Used for cost of edit operations.
        /// </summary>
        public readonly INode<T>[] PreLToNode;

        /// <summary>
        /// Index from left-to-right preorder id of node n (starting with {@code 0})
        /// to the size of n's subtree (node n and all its descendants).
        /// </summary>
        public readonly int[] Sizes;

        /// <summary>
        /// Index from left-to-right preorder id of node n (starting with {@code 0})
        /// to the left-to-right preorder id of n's parent.
        /// </summary>
        public readonly int[] Parents;

        /// <summary>
        /// Index from left-to-right preorder id of node n (starting with {@code 0})
        /// to the array of n's children. Size of children array at node n equals the number
        /// of n's children.
        /// </summary>
        public readonly int[][] Children;

        /// <summary>
        /// Index from left-to-right postorder id of node n (starting with {@code 0})
        /// to the left-to-right postorder id of n's leftmost leaf descendant.
        /// </summary>
        public readonly int[] PostLToLld;

        /// <summary>
        /// Index from right-to-left postorder id of node n (starting with {@code 0})
        /// to the right-to-left postorder id of n's rightmost leaf descendant.
        /// </summary>
        public readonly int[] PostRToRld;

        /// <summary>
        /// Index from left-to-right preorder id of node n (starting with {@code 0})
        /// to the left-to-right preorder id of the first leaf node to the left of n.
        /// If there is no leaf node to the left of n, it is represented with the
        /// value {@code -1} [1, Section 8.4].
        /// </summary>
        public readonly int[] PreLToLn;

        /// <summary>
        /// Index from right-to-left preorder id of node n (starting with {@code 0})
        /// to the right-to-left preorder id of the first leaf node to the right of n.
        /// If there is no leaf node to the right of n, it is represented with the
        /// value {@code -1} [1, Section 8.4].
        /// </summary>
        public readonly int[] PreRToLn;

        /// <summary>
        /// Index from left-to-right preorder id of node n (starting with {@code 0})
        /// to a bool value that states if node n lies on the leftmost path
        /// starting at n's parent [2, Algorithm 1, Lines 26,36].
        /// </summary>
        public readonly bool[] NodeTypeL;

        /// <summary>
        /// Index from left-to-right preorder id of node n (starting with {@code 0})
        /// to a bool value that states if node n lies on the rightmost path
        /// starting at n's parent input tree [2, Section 5.3, Algorithm 1, Lines 26,36].
        /// 
        /// </summary>
        public readonly bool[] NodeTypeR;

        /*
         * Traversal translation indices.
         */

        /// <summary>
        /// Index from left-to-right preorder id of node n (starting with {@code 0})
        /// to the right-to-left preorder id of n.
        /// </summary>
        public readonly int[] PreLToPreR;

        /// <summary>
        /// Index from right-to-left preorder id of node n (starting with {@code 0})
        /// to the left-to-right preorder id of n.
        /// </summary>
        public readonly int[] PreRToPreL;

        /// <summary>
        /// Index from left-to-right preorder id of node n (starting with {@code 0})
        /// to the left-to-right postorder id of n.
        /// </summary>
        public readonly int[] PreLToPostL;

        /// <summary>
        /// Index from left-to-right postorder id of node n (starting with {@code 0})
        /// to the left-to-right preorder id of n.
        /// </summary>
        public readonly int[] PostLToPreL;

        /// <summary>
        /// Index from left-to-right preorder id of node n (starting with {@code 0})
        /// to the right-to-left postorder id of n.
        /// </summary>
        public readonly int[] PreLToPostR;

        /// <summary>
        /// Index from right-to-left postorder id of node n (starting with {@code 0})
        /// to the left-to-right preorder id of n.
        /// </summary>
        public readonly int[] PostRToPreL;

        /*
         * Cost indices.
         */

        /// <summary>
        /// Index from left-to-right preorder id of node n (starting with {@code 0})
        /// to the cost of spf_L (single path function using the leftmost path) for
        /// the subtree rooted at n [1, Section 5.2].
        /// </summary>
        public readonly int[] PreLToKrSum;

        /// <summary>
        /// Index from left-to-right preorder id of node n (starting with {@code 0})
        /// to the cost of spf_R (single path function using the rightmost path) for
        /// the subtree rooted at n [1, Section 5.2].
        /// </summary>
        public readonly int[] PreLToRevKrSum;

        /// <summary>
        /// Index from left-to-right preorder id of node n (starting with {@code 0})
        /// to the cost of spf_A (single path function using an inner path) for the
        /// subtree rooted at n [1, Section 5.2].
        /// </summary>
        public readonly int[] PreLToDescSum;

        /// <summary>
        /// Index from left-to-right preorder id of node n (starting with {@code 0})
        /// to the cost of deleting all nodes in the subtree rooted at n.
        /// </summary>
        public readonly float[] PreLToSumDelCost;

        /// <summary>
        /// Index from left-to-right preorder id of node n (starting with {@code 0})
        /// to the cost of inserting all nodes in the subtree rooted at n.
        /// </summary>
        public readonly float[] PreLToSumInsCost;

        /*
         * Variables holding values modified at runtime while the algorithm executes.
         */

        /// <summary>
        /// Stores the left-to-right preorder id of the current subtree's root node.
        /// Used in the tree decomposition phase of APTED [1, Algorithm 1].
        /// </summary>
        private int _currentNode;

        /*
         * Structure single-value variables.
         */

        /// <summary>
        /// Stores the size of the input tree.
        /// </summary>
        private readonly int _treeSize;

        /// <summary>
        /// Stores the number of leftmost-child leaf nodes in the input tree [2, Section 5.3].
        /// </summary>
        public int NumberOfLeftMostChildLeadNodes;

        /// <summary>
        /// Stores the number of rightmost-child leaf nodes in the input tree [2, Section 5.3].
        /// </summary>
        public int NumberOfRightMostLeafNodes;

        /*
         * Variables used temporarily while indexing.
         */

        /// <summary>
        /// Temporary variable used in indexing for storing subtree size.
        /// </summary>
        private int _sizeTmp;

        /// <summary>
        /// Temporary variable used in indexing for storing sum of subtree sizes rooted at descendant nodes.
        /// </summary>
        private int _descSizesTmp;

        /// <summary>
        /// Temporary variable used in indexing for storing sum of key root node sizes.
        /// </summary>
        private int _krSizesSumTmp;

        /// <summary>
        /// Temporary variable used in indexing for storing sum of right-to-left key root node sizes.
        /// </summary>
        private int _revKrSizesSumTmp;

        /// <summary>
        /// Temporary variable used in indexing for storing preorder index of a node.
        /// </summary>
        private int _preorderTmp;

        /// <summary>
        /// The cost model
        /// </summary>
        private TCostModel _costModel;

        /// <summary>
        /// Indexes the nodes of input trees and stores the indices for quick access
        /// from APTED algorithm.
        /// </summary>
        /// <param name="inputTree">an input tree to APTED. Its nodes will be indexed.</param>
        /// <param name="costModel">instance of a cost model to compute preL_to_sumDelCost and preL_to_sumInsCost.</param>
        public NodeIndexer(INode<T> inputTree, TCostModel costModel)
        {
            // Initialise variables.
            _sizeTmp = 0;
            _descSizesTmp = 0;
            _krSizesSumTmp = 0;
            _revKrSizesSumTmp = 0;
            _preorderTmp = 0;
            _currentNode = 0;
            _treeSize = inputTree.GetNodeCount();

            // Initialise indices with the lengths equal to the tree size.
            Sizes = new int[_treeSize];
            PreLToPreR = new int[_treeSize];
            PreRToPreL = new int[_treeSize];
            PreLToPostL = new int[_treeSize];
            PostLToPreL = new int[_treeSize];
            PreLToPostR = new int[_treeSize];
            PostRToPreL = new int[_treeSize];
            PostLToLld = new int[_treeSize];
            PostRToRld = new int[_treeSize];
            PreLToNode = new INode<T>[_treeSize];
            PreLToLn = new int[_treeSize];
            PreRToLn = new int[_treeSize];
            PreLToKrSum = new int[_treeSize];
            PreLToRevKrSum = new int[_treeSize];
            PreLToDescSum = new int[_treeSize];

            PreLToSumDelCost = new float[_treeSize];
            PreLToSumInsCost = new float[_treeSize];

            Children = new int[_treeSize][];
            NodeTypeL = new bool[_treeSize];
            NodeTypeR = new bool[_treeSize];
            Parents = new int[_treeSize];
            Parents[0] = -1; // The root has no parent.

            _costModel = costModel;

            // Index the nodes.
            IndexNodes(inputTree, -1);
            PostTraversalIndexing();
        }

        /// <summary>
        /// Indexes the nodes of the input tree. Stores information about each tree
        /// node in index arrays. It computes the indices.
        ///
        /// It is a recursive method that traverses the tree once.
        ///
        /// </summary>
        /// <param name="node">node is the current node while traversing the input tree.</param>
        /// <param name="postorder">postorder is the postorder id of the current node.</param>
        /// <returns>postorder id of the current node.</returns>
        private int IndexNodes(INode<T> node, int postorder)
        {
            // Initialise variables.
            var currentSize = 0;
            var childrenCount = 0;
            var descSizes = 0;
            var krSizesSum = 0;
            var revKrSizesSum = 0;
            var preorder = _preorderTmp;

            // Initialise empty array to store children of this node.
            List<int> childrenPreorders = new();

            // Store the preorder id of the current node to use it after the recursion.
            _preorderTmp++;

            var nodeChildren = node.Children;
            if (nodeChildren != null)
            {
                for (var i = 0; i < nodeChildren.Count; i++)
                {
                    childrenCount++;
                    var currentPreorder = _preorderTmp;
                    Parents[currentPreorder] = preorder;

                    // Execute method recursively for next child.
                    postorder = IndexNodes(nodeChildren[i], postorder);

                    childrenPreorders.Add(currentPreorder);

                    currentSize += 1 + _sizeTmp;
                    descSizes += _descSizesTmp;
                    if (childrenCount > 1)
                    {
                        krSizesSum += _krSizesSumTmp + _sizeTmp + 1;
                    }
                    else
                    {
                        krSizesSum += _krSizesSumTmp;
                        NodeTypeL[currentPreorder] = true;
                    }

                    if (i + 1 < nodeChildren.Count) // Does it have a next item?
                    {
                        revKrSizesSum += _revKrSizesSumTmp + _sizeTmp + 1;
                    }
                    else
                    {
                        revKrSizesSum += _revKrSizesSumTmp;
                        NodeTypeR[currentPreorder] = true;
                    }
                }
            }

            postorder++;

            var currentDescSizes = descSizes + currentSize + 1;
            PreLToDescSum[preorder] = ((currentSize + 1) * (currentSize + 1 + 3)) / 2 - currentDescSizes;
            PreLToKrSum[preorder] = krSizesSum + currentSize + 1;
            PreLToRevKrSum[preorder] = revKrSizesSum + currentSize + 1;

            // Store pointer to a node object corresponding to preorder.
            PreLToNode[preorder] = node;

            Sizes[preorder] = currentSize + 1;
            var preorderR = _treeSize - 1 - postorder;
            PreLToPreR[preorder] = preorderR;
            PreRToPreL[preorderR] = preorder;

            Children[preorder] = childrenPreorders.ToArray();

            _descSizesTmp = currentDescSizes;
            _sizeTmp = currentSize;
            _krSizesSumTmp = krSizesSum;
            _revKrSizesSumTmp = revKrSizesSum;

            PostLToPreL[postorder] = preorder;
            PreLToPostL[preorder] = postorder;
            PreLToPostR[preorder] = _treeSize - 1 - preorder;
            PostRToPreL[_treeSize - 1 - preorder] = preorder;

            return postorder;
        }

        /// <summary>
        /// Indexes the nodes of the input tree. It computes the following indices,
        /// which could not be computed immediately while traversing the tree in
        /// {@link #indexNodes}: {@link #preL_to_ln}, {@link #postL_to_lld},
        /// {@link #postR_to_rld}, {@link #preR_to_ln}.
        ///
        /// Runs in linear time in the input tree size. Currently requires two
        /// loops over input tree nodes. Can be reduced to one loop (see the code).
        /// </summary>
        private void PostTraversalIndexing()
        {
            var currentLeaf = -1;
            for (var i = 0; i < _treeSize; i++)
            {
                PreLToLn[i] = currentLeaf;
                if (IsLeaf(i))
                {
                    currentLeaf = i;
                }

                // This block stores leftmost leaf descendants for each node
                // indexed in postorder. Used for mapping computation.
                // Added by Victor.
                var preorder = PostLToPreL[i];
                if (Sizes[preorder] == 1)
                {
                    PostLToLld[i] = i;
                }
                else
                {
                    PostLToLld[i] = PostLToLld[PreLToPostL[Children[preorder][0]]];
                }

                // This block stores rightmost leaf descendants for each node
                // indexed in right-to-left postorder.
                // [TODO] Use postL_to_lld and postR_to_rld instead of APTED.getLLD
                //        and APTED.gerRLD methods, remove these method.
                //        Result: faster lookup of these values.
                preorder = PostRToPreL[i];
                if (Sizes[preorder] == 1)
                {
                    PostRToRld[i] = i;
                }
                else
                {
                    PostRToRld[i] = PostRToRld[PreLToPostR[Children[preorder][Children[preorder].Length - 1]]];
                }

                // Count NumberOfLeftMostChildLeadNodes and NumberOfRightMostLeafNodes.
                // [TODO] There are no values for parent node.
                if (Sizes[i] == 1)
                {
                    var parent = Parents[i];
                    if (parent > -1)
                    {
                        if (parent + 1 == i)
                        {
                            NumberOfLeftMostChildLeadNodes++;
                        }
                        else if (PreLToPreR[parent] + 1 == PreLToPreR[i])
                        {
                            NumberOfRightMostLeafNodes++;
                        }
                    }
                }

                // Sum up costs of deleting and inserting entire subtrees.
                // Reverse the node index. Here, we need traverse nodes bottom-up.
                var nodeForSum = _treeSize - i - 1;
                var parentForSum = Parents[nodeForSum];
                // Update myself.
                PreLToSumDelCost[nodeForSum] += _costModel.Delete(PreLToNode[nodeForSum]);
                PreLToSumInsCost[nodeForSum] += _costModel.Insert(PreLToNode[nodeForSum]);
                if (parentForSum > -1)
                {
                    // Update my parent.
                    PreLToSumDelCost[parentForSum] += PreLToSumDelCost[nodeForSum];
                    PreLToSumInsCost[parentForSum] += PreLToSumInsCost[nodeForSum];
                }
            }

            currentLeaf = -1;

            // todo: Merge with the other loop. Assume different traversal.
            for (var i = 0; i < Sizes[0]; i++)
            {
                PreRToLn[i] = currentLeaf;
                if (IsLeaf(PreRToPreL[i]))
                {
                    currentLeaf = i;
                }
            }
        }

        /// <summary>
        /// An abbreviation that uses indices to calculate the left-to-right preorder
        /// id of the leftmost leaf node of the given node.
        /// </summary>
        /// <param name="preL">left-to-right preorder id of a node.</param>
        /// <returns>left-to-right preorder id of the leftmost leaf node of preL.</returns>
        public int preL_to_lld(int preL)
        {
            return PostLToPreL[PostLToLld[PreLToPostL[preL]]];
        }

        /// <summary>
        /// An abbreviation that uses indices to calculate the left-to-right preorder
        /// id of the rightmost leaf node of the given node.
        /// </summary>
        /// <param name="preL">left-to-right preorder id of a node.</param>
        /// <returns>left-to-right preorder id of the rightmost leaf node of preL.</returns>
        public int preL_to_rld(int preL)
        {
            return PostRToPreL[PostRToRld[PreLToPostR[preL]]];
        }

        /// <summary>
        /// An abbreviation that uses indices to retrieve pointer to {@link node.Node}
        /// of the given node.
        /// </summary>
        /// <param name="postL">left-to-right postorder id of a node.</param>
        /// <returns>{@link node.Node} corresponding to postL.</returns>
        public INode<T> postL_to_node(int postL)
        {
            return PreLToNode[PostLToPreL[postL]];
        }

        /// <summary>
        /// An abbreviation that uses indices to retrieve pointer to {@link node.Node}
        /// of the given node.
        /// </summary>
        /// <param name="postR">right-to-left postorder id of a node.</param>
        /// <returns>{@link node.Node} corresponding to postR.</returns>
        public INode<T> postR_to_node(int postR)
        {
            return PreLToNode[PostRToPreL[postR]];
        }

        /// <summary>
        /// Returns the number of nodes in the input tree.
        /// </summary>
        /// <returns>number of nodes in the tree.</returns>
        public int GetSize()
        {
            return _treeSize;
        }

        /// <summary>
        /// Verifies if node is a leaf.
        /// </summary>
        /// <param name="node">preorder id of a node to verify.</param>
        /// <returns>{@code true} if {@code node} is a leaf, {@code false} otherwise.</returns>
        public bool IsLeaf(int node)
        {
            return Sizes[node] == 1;
        }

        /// <summary>
        /// Returns the root node of the currently processed subtree in the tree
        /// decomposition part of APTED [1, Algorithm 1]. At each point, we have to
        /// know which subtree do we process.
        /// </summary>
        /// <returns>current subtree root node.</returns>
        public int GetCurrentNode()
        {
            return _currentNode;
        }

        /// <summary>
        /// Stores the root nodes preorder id of the currently processes subtree.
        /// </summary>
        /// <param name="preorder">preorder id of the root node.</param>
        public void SetCurrentNode(int preorder)
        {
            _currentNode = preorder;
        }
    }
}