# AptedSharp

## Overview

This is a C#/DotNet port of the [Java implementation](https://github.com/DatabaseGroup/apted) of the APTED algorithm for calculating tree edit distance.
The APTED algorithm is a state-of-the-art solution for computing the tree edit distance [1,2], which supersedes the RTED
algorithm [3].

You can find more information [tree edit distance](http://tree-edit-distance.dbresearch.uni-salzburg.at/) website.

## Licence

The source code is published under the **MIT licence** found in the root
directory of the project and in the header of each source file.

## Input

This version has been refactored so that existing tree models can be extended
to be used with this library.  For testing and demonstration purposes, there
is an included implementation that uses bracket notation for the input trees,
for example, encoding `{A{B{X}{Y}{F}}{C}}` corresponds to the following tree:
```
    A
   / \
  B   C
 /|\
X Y F
```

## Output

Our library computes two outputs:
- tree edit **distance** value - the minimum cost of transforming the source
  tree into the destination tree.
- tree edit **mapping** - a mapping between nodes that corresponds to the
  tree edit distance value. Nodes that are not mapped are deleted (source tree)
  or inserted (destination tree).

## Customising

If the nodes of your trees have complex updating/mapping costs, you can customise that.
There are three elements that you have to consider.
See the inline documentation for `ICostModel` for further details.

### Parsing the input

Our bundled parser `BracketNotationParser` takes the bracket-encoded input
tree as a string and transforms it to tree structure composed of `Node` objects.
If you'd like to use other encoding for string based trees, you have to write 
a custom class that implements `IInputParser` interface.

The parser creates nodes and stores the corresponding information in
`Node.NodeData`. The bundled implementation shows how to use `string` 
objects as your data type. If you need anything else, you have to 
implement your own class. It can be anything, you don't need to implement
an interface.

### Cost model

The cost model decides on the costs of edit operations for every node
(insertion and deletion) and every node pair (update). There is a bundled
implementation of a simple `StringCostModel` that returns `1` for deleting 
and inserting any node. The update cost depends on string equality.

Write a class that implements `ICostModel` interface if you need a more
sophisticated cost model. See `FixedCostModel` which is an example that
allows different pre-specified costs for each edit operation.

## References

1. M. Pawlik and N. Augsten. *Tree edit distance: Robust and memory-
   efficient*. Information Systems 56. 2016.

2. M. Pawlik and N. Augsten. *Efficient Computation of the Tree Edit
   Distance*. ACM Transactions on Database Systems (TODS) 40(1). 2015.

3. M. Pawlik and N. Augsten. *RTED: A Robust Algorithm for the Tree Edit
   Distance*. PVLDB 5(4). 2011.
