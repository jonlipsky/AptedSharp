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

using System.Text.Json.Serialization;

namespace AptedSharp.Tests.Model;

/// <summary>
/// This class represents a single test case from the JSON file. JSON keys are mapped to fields of this class.
/// </summary>
public class TestCase
{
    [JsonPropertyName("testId")] public int TestId { get; init; }

    [JsonPropertyName("t1")] public string T1 { get; init; } = null!;

    [JsonPropertyName("t2")] public string T2 { get; init; } = null!;

    [JsonPropertyName("d")] public int D { get; init; }

    public override string ToString()
    {
        return $"{nameof(TestId)}: {TestId}, {nameof(T1)}: {T1}, {nameof(T2)}: {T2}, {nameof(D)}: {D}";
    }
}