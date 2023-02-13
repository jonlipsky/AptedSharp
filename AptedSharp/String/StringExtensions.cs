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

namespace AptedSharp.String
{
    public static class StringExtensions
    {
        /// <summary>
        ///     Get the substring of a string between two indices (similar to how Java does it)
        /// </summary>
        /// <param name="value">The string value</param>
        /// <param name="startIndex">The start index</param>
        /// <param name="endIndex">The end index</param>
        /// <returns>The substring</returns>
        public static string SubstringBetweenIndices(
            this string value,
            int startIndex,
            int endIndex)
        {
            return value.Substring(startIndex, endIndex - startIndex);
        }
    }
}