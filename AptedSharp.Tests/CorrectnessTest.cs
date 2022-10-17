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

using System.Text.Json;
using AptedSharp.String;
using AptedSharp.Tests.Model;

namespace AptedSharp.Tests;

/// <summary>
/// Correctness unit tests of distance and mapping computation.
///
/// In case of mapping, only mapping cost is verified against the correct distance.
///
/// Currently tests only for unit-cost model and single string-value labels.
/// </summary>
[TestClass]
public class CorrectnessTest
{
    public static IEnumerable<object[]> GetData()
    {
        var stream = typeof(CorrectnessTest).Assembly.GetManifestResourceStream("AptedSharp.Tests.Resources.correctness.json");
        if (stream == null) throw new Exception("Unable to load the test cases from the embedded resource.");
        var testCases = JsonSerializer.Deserialize<List<TestCase>>(stream);
        if (testCases == null) throw new Exception("The JSON file did not include any test cases.");
        foreach (var testCase in testCases)
            yield return new object[] { testCase };
    }

    /// <summary>
    /// Parse trees from bracket notation to {node.StringNodeData}, convert back to
    /// strings and verify equality with the input.
    /// </summary>
    /// <param name="testCase"></param>
    [TestMethod]
    [DynamicData(nameof(GetData), DynamicDataSourceType.Method)]
    public void ParsingBracketNotationToStringNodeData(TestCase testCase)
    {
        // Parse the input.
        var parser = new BracketNotationParser();
        var t1 = parser.FromString(testCase.T1);
        var t2 = parser.FromString(testCase.T2);
        Assert.AreEqual(testCase.T1, t1.ToBracketNotationString());
        Assert.AreEqual(testCase.T2, t2.ToBracketNotationString());
    }

    /// <summary>
    /// Compute TED for a single test case and compare to the correct value. Uses node
    /// labels with a single string value and unit cost model.
    /// </summary>
    /// <param name="testCase"></param>
    [TestMethod]
    [DynamicData(nameof(GetData), DynamicDataSourceType.Method)]
    public void DistanceUnitCostStringNodeDataCostModel(TestCase testCase)
    {
        // Parse the input.
        var parser = new BracketNotationParser();
        var t1 = parser.FromString(testCase.T1);
        var t2 = parser.FromString(testCase.T2);
        // Initialise APTED.
        var apted = new Apted<StringCostModel, string>(new StringCostModel());
        // This cast is safe due to unit cost.
        var result = (int)apted.ComputeEditDistance(t1, t2);
        Assert.AreEqual(testCase.D, result);
        // Verify the symmetric case.
        result = (int)apted.ComputeEditDistance(t2, t1);
        Assert.AreEqual(testCase.D, result);
    }

    /// <summary>
    /// Compute TED for a single test case and compare to the correct value. Uses
    /// node labels with a single string value and unit cost model.
    ///
    /// Triggers spf_L to execute. The strategy is fixed to left paths in the
    /// left-hand tree.
    /// </summary>
    /// <param name="testCase"></param>
    [TestMethod]
    [DynamicData(nameof(GetData), DynamicDataSourceType.Method)]
    public void DistanceUnitCostStringNodeDataCostModelSpfL(TestCase testCase)
    {
        // Parse the input.
        var parser = new BracketNotationParser();
        var t1 = parser.FromString(testCase.T1);
        var t2 = parser.FromString(testCase.T2);
        // Initialise APTED.
        var apted = new Apted<StringCostModel, string>(new StringCostModel());
        // This cast is safe due to unit cost.
        var result = (int)apted.computeEditDistance_spfTest(t1, t2, 0);
        Assert.AreEqual(testCase.D, result);
    }

    /// <summary>
    /// Compute TED for a single test case and compare to the correct value. Uses
    /// node labels with a single string value and unit cost model.
    ///
    /// Triggers spf_R to execute. The strategy is fixed to right paths in the
    /// left-hand tree.
    /// </summary>
    /// <param name="testCase"></param>
    [TestMethod]
    [DynamicData(nameof(GetData), DynamicDataSourceType.Method)]
    public void DistanceUnitCostStringNodeDataCostModelSpfR(TestCase testCase)
    {
        // Parse the input.
        var parser = new BracketNotationParser();
        var t1 = parser.FromString(testCase.T1);
        var t2 = parser.FromString(testCase.T2);
        // Initialise APTED.
        var apted = new Apted<StringCostModel, string>(new StringCostModel());
        // This cast is safe due to unit cost.
        var result = (int)apted.computeEditDistance_spfTest(t1, t2, 1);
        Assert.AreEqual(testCase.D, result);
    }

    /// <summary>
    /// Compute minimum-cost edit mapping for a single test case and compare its
    /// cost to the correct TED value. Uses node labels with a single string value
    /// and unit cost model.
    /// </summary>
    /// <param name="testCase"></param>
    [TestMethod]
    [DynamicData(nameof(GetData), DynamicDataSourceType.Method)]
    public void MappingCostUnitCostStringNodeDataCostModel(TestCase testCase)
    {
        // Parse the input.
        var parser = new BracketNotationParser();
        var t1 = parser.FromString(testCase.T1);
        var t2 = parser.FromString(testCase.T2);
        // Initialise APTED.
        var apted = new Apted<StringCostModel, string>(new StringCostModel());
        // Although we don't need TED value yet, TED must be computed before the
        // mapping. This cast is safe due to unit cost.
        apted.ComputeEditDistance(t1, t2);
        // Get TED value corresponding to the computed mapping.
        var mapping = apted.ComputeEditMapping();
        // This cast is safe due to unit cost.
        var result = (int)apted.MappingCost(mapping);
        Assert.AreEqual(testCase.D, result);
    }
}