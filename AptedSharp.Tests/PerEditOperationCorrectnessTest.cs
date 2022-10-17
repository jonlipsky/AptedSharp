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
/// Correctness unit tests of distance computation for node labels with a
/// single string value and per-edit-operation cost model.
/// </summary>
[TestClass]
public class PerEditOperationCorrectnessTest
{
    public static IEnumerable<object[]> GetData()
    {
        var stream = typeof(CorrectnessTest).Assembly.GetManifestResourceStream("AptedSharp.Tests.Resources.per_edit_operation_correctness.json");
        if (stream == null) throw new Exception("Unable to load the test cases from the embedded resource.");
        var testCases = JsonSerializer.Deserialize<List<TestCase>>(stream);
        if (testCases == null) throw new Exception("The JSON file did not include any test cases.");
        foreach (var testCase in testCases)
            yield return new object[] { testCase };
    }

    /// <summary>
    /// Compute TED for a single test case and compare to the correct value. Uses node
    /// labels with a single string value and per-edit-operation cost model.
    ///
    /// The correct value is calculated using AllPossibleMappingsTED algorithm.
    ///
    /// The costs of edit operations are set to some example values different than in the unit cost model.
    /// </summary>
    /// <param name="testCase"></param>
    [TestMethod]
    [DynamicData(nameof(GetData), DynamicDataSourceType.Method)]
    public void DistancePerEditOperationStringNodeDataCostModel(TestCase testCase)
    {
        // Parse the input.
        var parser = new BracketNotationParser();
        var t1 = parser.FromString(testCase.T1);
        var t2 = parser.FromString(testCase.T2);
        // Initialise algorithms.
        var apted = new Apted<FixedCostModel<string>, string>(new FixedCostModel<string>(0.4f, 0.4f, 0.6f));
        var apmted = new AllPossibleMappingsTed<FixedCostModel<string>, string>(new FixedCostModel<string>(0.4f, 0.4f, 0.6f));
        // Calculate distances using both algorithms.
        var result = apted.ComputeEditDistance(t1, t2);
        var correctResult = apmted.ComputeEditDistance(t1, t2);
        Assert.AreEqual(correctResult, result, 0.0001f);
    }
}