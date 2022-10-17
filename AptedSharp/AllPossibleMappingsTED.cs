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

namespace AptedSharp;

/// <summary>
/// Implements an exponential algorithm for the tree edit distance. It computes
/// all possible TED mappings between two trees and calculated their minimal
/// cost.
/// </summary>
/// <typeparam name="TCostModel">type of cost model.</typeparam>
/// <typeparam name="T">type of node data.</typeparam>
public class AllPossibleMappingsTed<TCostModel, T> where TCostModel : ICostModel<T>
{
    /// <summary>
    /// Indexer of the source tree.
    /// </summary>
    private NodeIndexer<T, TCostModel> _it1;

    /// <summary>
    /// Indexer of the destination tree.
    /// </summary>
    private NodeIndexer<T, TCostModel> _it2;

    /// <summary>
    /// The size of the source input tree.
    /// </summary>
    private int _size1;

    /// <summary>
    /// The size of the destination tree.
    /// </summary>
    private int _size2;

    /// <summary>
    /// Cost model to be used for calculating costs of edit operations.
    /// </summary>
    private TCostModel _costModel;

    /// <summary>
    /// Constructs the AllPossibleMappingsTED algorithm with a specific cost model.
    /// </summary>
    /// <param name="costModel">a cost model used in the algorithm.</param>
    public AllPossibleMappingsTed(TCostModel costModel)
    {
        _costModel = costModel;
    }

    /// <summary>
    /// Computes the tree edit distance between two trees by trying all possible
    /// TED mappings. It uses the specified cost model.
    /// </summary>
    /// <param name="t1">source tree.</param>
    /// <param name="t2">destination tree.</param>
    /// <returns>the tree edit distance between two trees.</returns>
    public float ComputeEditDistance(INode<T> t1, INode<T> t2)
    {
        // Index the nodes of both input trees.
        Init(t1, t2);
        var mappings = GenerateAllOneToOneMappings();
        RemoveNonTedMappings(mappings);
        return GetMinCost(mappings);
    }

    /// <summary>
    /// Indexes the input trees.
    /// </summary>
    /// <param name="t1">source tree.</param>
    /// <param name="t2">destination tree.</param>
    private void Init(INode<T> t1, INode<T> t2)
    {
        _it1 = new NodeIndexer<T, TCostModel>(t1, _costModel);
        _it2 = new NodeIndexer<T, TCostModel>(t2, _costModel);
        _size1 = _it1.GetSize();
        _size2 = _it2.GetSize();
    }

    /// <summary>
    /// Generate all possible 1-1 mappings.
    ///
    /// These mappings do not conform to TED conditions (sibling-order and
    /// ancestor-descendant).
    ///
    /// A mapping is a list of pairs (arrays) of preorder IDs (identifying
    /// nodes).
    /// </summary>
    /// <returns>set of all 1-1 mappings.</returns>
    private List<List<int[]>> GenerateAllOneToOneMappings()
    {
        // Start with an empty mapping - all nodes are deleted or inserted.
        var mappings = new List<List<int[]>>(1)
        {
            new(_size1 + _size2)
        };

        // Add all deleted nodes.
        for (var n1 = 0; n1 < _size1; n1++)
        {
            mappings[0].Add(new[] { n1, -1 });
        }

        // Add all inserted nodes.
        for (var n2 = 0; n2 < _size2; n2++)
        {
            mappings[0].Add(new[] { -1, n2 });
        }

        // For each node in the source tree.
        for (var n1 = 0; n1 < _size1; n1++)
        {
            // Duplicate all mappings and store in mappings_copy.
            var mappingsCopy = DeepMappingsCopy(mappings);
            // For each node in the destination tree.
            for (var n2 = 0; n2 < _size2; n2++)
            {
                // For each mapping (produced for all n1 values smaller than
                // current n1).
                foreach (var m in mappingsCopy)
                {
                    // Produce new mappings with the pair (n1, n2) by adding this
                    // pair to all mappings where it is valid to add.
                    var elementAdd = true;
                    // Verify if (n1, n2) can be added to mapping m.
                    // All elements in m are checked with (n1, n2) for possible
                    // violation.
                    // One-to-one condition.
                    foreach (var e in m)
                    {
                        // n1 is not in any of previous mappings
                        if (e[0] != -1 && e[1] != -1 && e[1] == n2)
                        {
                            elementAdd = false;
                            // Console.WriteLine("Add " + n2 + " false.");
                            break;
                        }
                    }

                    // New mappings must be produced by duplicating a previous
                    // mapping and extending it by (n1, n2).
                    if (elementAdd)
                    {
                        List<int[]> mCopy = DeepMappingCopy(m);
                        mCopy.Add(new[] { n1, n2 });
                        // If a pair (n1,n2) is added, (n1,-1) and (-1,n2) must be removed.
                        RemoveMappingElement(mCopy, new [] { n1, -1 });
                        RemoveMappingElement(mCopy, new [] { -1, n2 });
                        mappings.Add(mCopy);
                    }
                }
            }
        }

        return mappings;
    }

    /// <summary>
    /// Given all 1-1 mappings, discard these that violate TED conditions
    /// (ancestor-descendant and sibling order).
    /// </summary>
    /// <param name="mappings">mappings set of all 1-1 mappings.</param>
    private void RemoveNonTedMappings(List<List<int[]>> mappings)
    {
        // Validate each mapping separately.
        for (var i = 0; i < mappings.Count; i++)
        {
            var m = mappings[i];
            if (!IsTedMapping(m))
            {
                mappings.Remove(m);
                i--;
            }
        }
    }

    /// <summary>
    /// Test if a 1-1 mapping is a TED mapping.
    /// </summary>
    /// <param name="m">a 1-1 mapping.</param>
    /// <returns>{@code true} if {@code m} is a TED mapping, and {@code false} otherwise.</returns>
    private bool IsTedMapping(List<int[]> m)
    {
        // Validate each pair of pairs of mapped nodes in the mapping.
        foreach (var e1 in m)
        {
            // Use only pairs of mapped nodes for validation.
            if (e1[0] == -1 || e1[1] == -1)
            {
                continue;
            }

            foreach (var e2 in m)
            {
                // Use only pairs of mapped nodes for validation.
                if (e2[0] == -1 || e2[1] == -1)
                {
                    continue;
                }

                // If any of the conditions below doesn't hold, discard m.
                // Validate ancestor-descendant condition.
                var a = e1[0] < e2[0] && _it1.PreLToPreR[e1[0]] < _it1.PreLToPreR[e2[0]];
                var b = e1[1] < e2[1] && _it2.PreLToPreR[e1[1]] < _it2.PreLToPreR[e2[1]];
                if ((a && !b) || (!a && b))
                {
                    // Discard the mapping.
                    // If this condition doesn't hold, the next condition
                    // doesn't have to be verified any more and any other
                    // pair (e1, e2) doesn't have to be verified any more.
                    return false;
                }

                // Validate sibling-order condition.
                a = e1[0] < e2[0] && _it1.PreLToPreR[e1[0]] > _it1.PreLToPreR[e2[0]];
                b = e1[1] < e2[1] && _it2.PreLToPreR[e1[1]] > _it2.PreLToPreR[e2[1]];
                if ((a && !b) || (!a && b))
                {
                    // Discard the mapping.
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Given list of all TED mappings, calculate the cost of the minimal-cost
    /// mapping.
    /// </summary>
    /// <param name="tedMappings">set of all TED mappings.</param>
    /// <returns>the minimal cost among all TED mappings.</returns>
    float GetMinCost(List<List<int[]>> tedMappings)
    {
        // Initialize min_cost to the upper bound.
        float minCost = _size1 + _size2;
        // Console.WriteLine("min_cost = " + min_cost);
        // Verify cost of each mapping.
        foreach (var m in tedMappings)
        {
            float mCost = 0;
            // Sum up edit costs for all elements in the mapping m.
            foreach (var e in m)
            {
                // Add edit operation cost.
                if (e[0] > -1 && e[1] > -1)
                {
                    mCost += _costModel.Update(_it1.PreLToNode[e[0]], _it2.PreLToNode[e[1]]); // USE COST MODEL - update e[0] to e[1].
                }
                else if (e[0] > -1)
                {
                    mCost += _costModel.Delete(_it1.PreLToNode[e[0]]); // USE COST MODEL - insert e[1].
                }
                else
                {
                    mCost += _costModel.Insert(_it2.PreLToNode[e[1]]); // USE COST MODEL - delete e[0].
                }

                // Break as soon as the current min_cost is exceeded.
                // Only for early loop break.
                if (mCost >= minCost)
                {
                    break;
                }
            }

            // Store the minimal cost - compare m_cost and min_cost
            if (mCost < minCost)
            {
                minCost = mCost;
            }
            // System.out.printf("min_cost = %.8f\n", min_cost);
        }

        return minCost;
    }

    /// <summary>
    /// Makes a deep copy of a mapping.
    /// </summary>
    /// <param name="mapping">mapping mapping to copy.</param>
    /// <returns>a mapping.</returns>
    private List<int[]> DeepMappingCopy(List<int[]> mapping)
    {
        var mappingCopy = new List<int[]>(mapping.Count);
        foreach (var me in mapping)
        {
            // for each mapping element in a mapping
            var copy = new int[me.Length];
            Array.Copy(me, copy, me.Length);
            mappingCopy.Add(copy);
        }

        return mappingCopy;
    }

    /// <summary>
    /// Makes a deep copy of a set of mappings.
    /// </summary>
    /// <param name="mappings">mappings set of mappings to copy.</param>
    /// <returns>set of mappings.</returns>
    private List<List<int[]>> DeepMappingsCopy(List<List<int[]>> mappings)
    {
        var mappingsCopy = new List<List<int[]>>(mappings.Count);
        foreach (var m in mappings)
        {
            // for each mapping in mappings
            var mCopy = new List<int[]>(m.Count);
            foreach (var me in m)
            {
                // for each mapping element in a mapping
                var copy = new int[me.Length];
                Array.Copy(me, copy, me.Length);
                mCopy.Add(copy);
            }

            mappingsCopy.Add(mCopy);
        }

        return mappingsCopy;
    }

    /// <summary>
    /// Constructs a string representation of a set of mappings.
    /// </summary>
    /// <param name="mappings">mappings set of mappings to convert.</param>
    /// <returns>string representation of a set of mappings.</returns>
    private string MappingsToString(List<List<int[]>> mappings)
    {
        var result = "Mappings:\n";
        foreach (var m in mappings)
        {
            result += "{";
            foreach (var me in m)
            {
                result += "[" + me[0] + "," + me[1] + "]";
            }

            result += "}\n";
        }

        return result;
    }

    /// <summary>
    /// Removes an element (edit operation) from a mapping by its value. In our
    /// case the element to remove can be always found in the mapping.
    /// </summary>
    /// <param name="m">an edit mapping.</param>
    /// <param name="e">element to remove from {@code m}.</param>
    /// <returns>{@code true} if {@code e} has been removed, and {@code false} otherwise.</returns>
    private bool RemoveMappingElement(List<int[]> m, int[] e)
    {
        foreach (var me in m)
        {
            if (me[0] == e[0] && me[1] == e[1])
            {
                m.Remove(me);
                return true;
            }
        }

        return false;
    }
}