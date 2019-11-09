/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * .Net Core
 * The MIT License (MIT)
 * 
 * Copyright (c) .NET Foundation and Contributors
 * 
 * All rights reserved.
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AI4E.Utils.Memory
{
    public static class Base64Coder
    {
        #region Fields

        private const string _badBase64Char = "The input is not a valid Base-64 string as it contains a non-base 64 character, more than two padding characters, or an illegal character among the padding characters.";
        private const string _illegalEnumValue = "Illegal enum value: {0}.";
        private const byte _encodingPad = (byte)'='; // '=', for padding
        private const int _base64LineBreakPosition = 76;

        // Pre-computing this table using a custom string(s_characters) and GenerateDecodingMapAndVerify (found in tests)
        private static readonly sbyte[] _decodingMap = {
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 62, -1, -1, -1, 63,         //62 is placed at index 43 (for +), 63 at index 47 (for /)
            52, 53, 54, 55, 56, 57, 58, 59, 60, 61, -1, -1, -1, -1, -1, -1,         //52-61 are placed at index 48-57 (for 0-9), 64 at index 61 (for =)
            -1,  0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14,
            15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, -1, -1, -1, -1, -1,         //0-25 are placed at index 65-90 (for A-Z)
            -1, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40,
            41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, -1, -1, -1, -1, -1,         //26-51 are placed at index 97-122 (for a-z)
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,         // Bytes over 122 ('z') are invalid and cannot be decoded
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,         // Hence, padding the map with 255, which indicates invalid input
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        };

        private static readonly char[] _base64Table = {'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O',
                                                        'P','Q','R','S','T','U','V','W','X','Y','Z','a','b','c','d',
                                                        'e','f','g','h','i','j','k','l','m','n','o','p','q','r','s',
                                                        't','u','v','w','x','y','z','0','1','2','3','4','5','6','7',
                                                        '8','9','+','/','=' };

        private static readonly char[] _whitespaceChars = { ' ', '\t', '\r', '\n' };

        #endregion

        #region ToBase64Chars

        public static bool TryToBase64Chars(ReadOnlySpan<byte> bytes,
                                            Span<char> chars,
                                            out int charsWritten,
                                            Base64FormattingOptions options = Base64FormattingOptions.None)
        {
            if (options < Base64FormattingOptions.None || options > Base64FormattingOptions.InsertLineBreaks)
            {
                throw new ArgumentException(string.Format(_illegalEnumValue, (int)options), nameof(options));
            }

            if (bytes.Length == 0)
            {
                charsWritten = 0;
                return true;
            }

            var charLengthRequired = ToBase64_CalculateAndValidateOutputLength(bytes.Length, options);
            if (charLengthRequired > chars.Length)
            {
                charsWritten = 0;
                return false;
            }

            charsWritten = ConvertToBase64Array(chars, bytes, 0, bytes.Length, options);
            return true;
        }

        public static Span<char> ToBase64Chars(ReadOnlySpan<byte> bytes,
                                               Span<char> chars,
                                               Base64FormattingOptions options = Base64FormattingOptions.None)
        {
            if (options < Base64FormattingOptions.None || options > Base64FormattingOptions.InsertLineBreaks)
            {
                throw new ArgumentException(string.Format(_illegalEnumValue, (int)options), nameof(options));
            }

            if (bytes.Length == 0)
            {
                return Span<char>.Empty;
            }

            var charLengthRequired = ToBase64_CalculateAndValidateOutputLength(bytes.Length, options);
            if (charLengthRequired > chars.Length)
            {
                throw new ArgumentException("insufficient memory", nameof(bytes)); // TODO: Exception message
            }

            var charsWritten = ConvertToBase64Array(chars, bytes, 0, bytes.Length, options);
            return chars.Slice(start: 0, length: charsWritten);
        }

        public static ReadOnlySpan<char> ToBase64Chars(ReadOnlySpan<byte> bytes,
                                                       Base64FormattingOptions options = Base64FormattingOptions.None)
        {
            if (options < Base64FormattingOptions.None || options > Base64FormattingOptions.InsertLineBreaks)
            {
                throw new ArgumentException(string.Format(_illegalEnumValue, (int)options), nameof(options));
            }

            if (bytes.Length == 0)
            {
                return ReadOnlySpan<char>.Empty;
            }

            var result = new char[ToBase64_CalculateAndValidateOutputLength(bytes.Length, options)].AsMemory();
            var charsWritten = ConvertToBase64Array(result.Span, bytes, 0, bytes.Length, options);
            Debug.Assert(result.Length == charsWritten, $"Expected {result.Length} == {charsWritten}");

            return result.Span;
        }

        public static string ToBase64String(
            ReadOnlySpan<byte> bytes, Base64FormattingOptions options = Base64FormattingOptions.None)
        {
            if (bytes.IsEmpty)
            {
                return string.Empty;
            }

            var resultLength = ComputeBase64EncodedLength(bytes, options);

            if (resultLength == 0)
            {
                return string.Empty;
            }

            var result = new string('\0', resultLength);
            var memory = MemoryMarshal.AsMemory(result.AsMemory());

            var charsLength = ToBase64Chars(bytes, memory.Span, options).Length;

            Debug.Assert(charsLength == resultLength);

            return result;
        }

        private static int ConvertToBase64Array(
            Span<char> chars, ReadOnlySpan<byte> bytes, int offset, int length, Base64FormattingOptions options)
        {
            var insertLineBreaks = (options & Base64FormattingOptions.InsertLineBreaks) != 0;
            var lengthmod3 = length % 3;
            var calcLength = offset + (length - lengthmod3);
            var j = 0;
            var charcount = 0;
            //Convert three bytes at a time to base64 notation.  This will consume 4 chars.
            int i;

            for (i = offset; i < calcLength; i += 3)
            {
                if (insertLineBreaks)
                {
                    if (charcount == _base64LineBreakPosition)
                    {
                        chars[j++] = '\r';
                        chars[j++] = '\n';
                        charcount = 0;
                    }
                    charcount += 4;
                }
                chars[j] = _base64Table[(bytes[i] & 0xfc) >> 2];
                chars[j + 1] = _base64Table[((bytes[i] & 0x03) << 4) | ((bytes[i + 1] & 0xf0) >> 4)];
                chars[j + 2] = _base64Table[((bytes[i + 1] & 0x0f) << 2) | ((bytes[i + 2] & 0xc0) >> 6)];
                chars[j + 3] = _base64Table[bytes[i + 2] & 0x3f];
                j += 4;
            }

            //Where we left off before
            i = calcLength;

            if (insertLineBreaks && lengthmod3 != 0 && charcount == _base64LineBreakPosition)
            {
                chars[j++] = '\r';
                chars[j++] = '\n';
            }

            switch (lengthmod3)
            {
                case 2: //One character padding needed
                    chars[j] = _base64Table[(bytes[i] & 0xfc) >> 2];
                    chars[j + 1] = _base64Table[((bytes[i] & 0x03) << 4) | ((bytes[i + 1] & 0xf0) >> 4)];
                    chars[j + 2] = _base64Table[(bytes[i + 1] & 0x0f) << 2];
                    chars[j + 3] = _base64Table[64]; //Pad
                    j += 4;
                    break;
                case 1: // Two character padding needed
                    chars[j] = _base64Table[(bytes[i] & 0xfc) >> 2];
                    chars[j + 1] = _base64Table[(bytes[i] & 0x03) << 4];
                    chars[j + 2] = _base64Table[64]; //Pad
                    chars[j + 3] = _base64Table[64]; //Pad
                    j += 4;
                    break;
            }

            return j;
        }

        public static int ComputeBase64EncodedLength(
            ReadOnlySpan<byte> bytes, Base64FormattingOptions options = Base64FormattingOptions.None)
        {
            return ToBase64_CalculateAndValidateOutputLength(bytes.Length, options);
        }

        private static int ToBase64_CalculateAndValidateOutputLength(int inputLength, Base64FormattingOptions options)
        {
            var insertLineBreaks = (options & Base64FormattingOptions.InsertLineBreaks) != 0;
            var outlen = (long)inputLength / 3 * 4;          // the base length - we want integer division here. 
            outlen += (inputLength % 3 != 0) ? 4 : 0;         // at most 4 more chars for the remainder

            if (outlen == 0)
                return 0;

            if (insertLineBreaks)
            {
                var newLines = outlen / _base64LineBreakPosition;
                if (outlen % _base64LineBreakPosition == 0)
                {
                    --newLines;
                }
                outlen += newLines * 2;              // the number of line break chars we'll add, "\r\n"
            }

            // If we overflow an int then we cannot allocate enough
            // memory to output the value so throw
            if (outlen > int.MaxValue)
                throw new OutOfMemoryException();

            return (int)outlen;
        }

        #endregion

        #region FromBase64Chars

        public static bool TryFromBase64Chars(ReadOnlySpan<char> chars, Span<byte> bytes, out int bytesWritten)
        {
#if NETCORE
            return System.Convert.TryFromBase64Chars(chars, bytes, out bytesWritten);
#endif

            // This is actually local to one of the nested blocks
            // but is being declared at the top as we don't want multiple stackallocs
            // for each iteraton of the loop.
            // Note: The tempBuffer size could be made larger than 4 but the size must be a multiple of 4.
            Span<char> tempBuffer = stackalloc char[4];

            bytesWritten = 0;

            while (chars.Length != 0)
            {
                // Attempt to decode a segment that doesn't contain whitespace.
                var complete = TryDecodeFromUtf16(
                    chars, bytes, out var consumedInThisIteration, out var bytesWrittenInThisIteration);
                bytesWritten += bytesWrittenInThisIteration;
                if (complete)
                    return true;

                chars = chars.Slice(consumedInThisIteration);
                bytes = bytes.Slice(bytesWrittenInThisIteration);

                // If TryDecodeFromUtf16() consumed the entire buffer, it could not have returned false.
                Debug.Assert(chars.Length != 0);
                if (chars[0].IsSpace())
                {
                    // If we got here, the very first character not consumed was a whitespace.
                    // We can skip past any consecutive whitespace, then continue decoding.

                    var indexOfFirstNonSpace = 1;
                    for (; ; )
                    {
                        if (indexOfFirstNonSpace == chars.Length)
                            break;

                        if (!chars[indexOfFirstNonSpace].IsSpace())
                            break;

                        indexOfFirstNonSpace++;
                    }

                    chars = chars.Slice(indexOfFirstNonSpace);

                    if (bytesWrittenInThisIteration % 3 != 0 && chars.Length != 0)
                    {
                        // If we got here, the last successfully decoded block encountered an end-marker,
                        // yet we have trailing non-whitespace characters. That is not allowed.
                        bytesWritten = default;
                        return false;
                    }

                    // We now loop again to decode the next run of non-space characters. 
                }
                else
                {
                    Debug.Assert(chars.Length != 0 && !chars[0].IsSpace());

                    // If we got here, it is possible that there is whitespace that occurred in the middle of a
                    // 4-byte chunk. That is, we still have up to three Base64 characters that were left undecoded
                    // by the fast-path helper because they didn't form a complete 4-byte chunk. This is hopefully the
                    // rare case (multiline-formatted base64 message with a non-space character width that's not a
                    // multiple of 4.) We'll filter out whitespace and copy the remaining characters into a temporary
                    // buffer.
                    CopyToTempBufferWithoutWhiteSpace(
                        chars, tempBuffer, out var consumedFromChars, out var charsWritten);
                    if ((charsWritten & 0x3) != 0)
                    {
                        // Even after stripping out whitespace, the number of characters is not divisible by 4.
                        // This cannot be a legal Base64 string.
                        bytesWritten = default;
                        return false;
                    }

                    tempBuffer = tempBuffer.Slice(0, charsWritten);
                    if (!TryDecodeFromUtf16(
                        tempBuffer, bytes, out var consumedFromTempBuffer, out var bytesWrittenFromTempBuffer))
                    {
                        bytesWritten = default;
                        return false;
                    }
                    bytesWritten += bytesWrittenFromTempBuffer;
                    chars = chars.Slice(consumedFromChars);
                    bytes = bytes.Slice(bytesWrittenFromTempBuffer);

                    if (bytesWrittenFromTempBuffer % 3 != 0)
                    {
                        // If we got here, this decode contained one or more padding characters ('=').
                        // We can accept trailing whitespace after this but nothing else.
                        for (var i = 0; i < chars.Length; i++)
                        {
                            if (!chars[i].IsSpace())
                            {
                                bytesWritten = default;
                                return false;
                            }
                        }
                        return true;
                    }

                    // We now loop again to decode the next run of non-space characters. 
                }
            }

            return true;
        }

        public static Span<byte> TryFromBase64Chars(ReadOnlySpan<char> chars, Span<byte> bytes)
        {
            if (TryFromBase64Chars(chars, bytes, out var bytesWritten))
            {
                return bytes.Slice(start: 0, length: bytesWritten);
            }

            throw new FormatException(_badBase64Char);
        }

        public static ReadOnlySpan<byte> FromBase64Chars(ReadOnlySpan<char> chars)
        {
            if (chars.IsEmpty)
            {
                return ReadOnlySpan<byte>.Empty;
            }

            return InternalFromBase64Chars(chars, bytes: default, bytesPresent: false);
        }

        public static Span<byte> FromBase64Chars(ReadOnlySpan<char> chars, Span<byte> bytes)
        {
            if (chars.IsEmpty)
            {
                return Span<byte>.Empty;
            }

            return InternalFromBase64Chars(chars, bytes, bytesPresent: true);
        }

        /// <summary>
        /// Computes the number of bytes that the base64 decoded data consumes at most.
        /// </summary>
        /// <param name="chars">The base64 encoded data.</param>
        /// <returns>The number of bytes the base64 encoded data contains at most.</returns>
        public static int ComputeBase64DecodedLength(ReadOnlySpan<char> chars)
        {
            const int padding = 2;
            return chars.Length / 4 * 3 + padding;
        }

        /// <summary>
        /// Convert Base64 encoding characters to bytes:
        ///  - Compute result length exactly by actually walking the input;
        ///  - Allocate new result array based on computation;
        ///  - Decode input into the new array;
        /// </summary>
        /// <param name="inputPtr">Pointer to the first input char</param>
        /// <param name="inputLength">Number of input chars</param>
        /// <returns></returns>
        private static Span<byte> InternalFromBase64Chars(ReadOnlySpan<char> chars, Span<byte> bytes, bool bytesPresent)
        {
            // The validity of parameters much be checked by callers, thus we are Critical here.

            Debug.Assert(!chars.IsEmpty);

            // We need to get rid of any trailing white spaces.
            // Otherwise we would be rejecting input such as "abc= ":
            chars = chars.TrimEnd(_whitespaceChars);

            // Compute the output length:
            var resultLength = InternalComputeBase64DecodedLength(chars);

            Debug.Assert(resultLength >= 0);

            // resultLength can be zero. We will still enter FromBase64_Decode and process the input.
            // It may either simply write no bytes (e.g. input = " ") or throw (e.g. input = "ab").

            if (!bytesPresent)
            {
                bytes = new byte[resultLength];
            }
            else if (bytes.Length < resultLength)
            {
                throw new ArgumentException("insufficient memory", nameof(bytes)); // TODO: Exception message
            }

            Debug.Assert(bytes.Length >= resultLength);

            // Convert Base64 chars into bytes:
            if (!TryFromBase64Chars(chars, bytes, out var _))
            {
                throw new FormatException(_badBase64Char);
            }

            // Note that the number of bytes written can differ from resultLength if the caller is modifying the array
            // as it is being converted. Silently ignore the failure.
            // Consider throwing exception in an non in-place release.

            // We are done:
            return bytes.Slice(start: 0, length: resultLength);
        }

        /// <summary>
        /// Compute the number of bytes encoded in the specified Base 64 char array:
        /// Walk the entire input counting white spaces and padding chars, then compute result length
        /// based on 3 bytes per 4 chars.
        /// </summary>
        private static int InternalComputeBase64DecodedLength(ReadOnlySpan<char> chars)
        {
            const uint intEq = '=';
            const uint intSpace = ' ';

            Debug.Assert(!chars.IsEmpty);

            var usefulInputLength = chars.Length;
            var padding = 0;

            for (var i = 0; i < chars.Length; i++)
            {
                var c = (uint)chars[i];

                // We want to be as fast as possible and filter out spaces with as few comparisons as possible.
                // We end up accepting a number of illegal chars as legal white-space chars.
                // This is ok: as soon as we hit them during actual decode we will recognise them as illegal and throw.
                if (c <= intSpace)
                {
                    usefulInputLength--;
                }
                else if (c == intEq)
                {
                    usefulInputLength--;
                    padding++;
                }
            }

            Debug.Assert(usefulInputLength >= 0);

            // For legal input, we can assume that 0 <= padding < 3. But it may be more for illegal input.
            // We will notice it at decode when we see a '=' at the wrong place.
            Debug.Assert(padding >= 0);

            // Perf: reuse the variable that stored the number of '=' to store the number of bytes encoded by the
            // last group that contains the '=':
            if (padding != 0)
            {
                if (padding == 1)
                {
                    padding = 2;
                }
                else if (padding == 2)
                {
                    padding = 1;
                }
                else
                {
                    throw new FormatException(_badBase64Char);
                }
            }

            // Done:
            return usefulInputLength / 4 * 3 + padding;
        }

        #endregion

        private static void CopyToTempBufferWithoutWhiteSpace(
            ReadOnlySpan<char> chars, Span<char> tempBuffer, out int consumed, out int charsWritten)
        {
            Debug.Assert(tempBuffer.Length != 0); // We only bound-check after writing a character to the tempBuffer.

            charsWritten = 0;
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (!c.IsSpace())
                {
                    tempBuffer[charsWritten++] = c;
                    if (charsWritten == tempBuffer.Length)
                    {
                        consumed = i + 1;
                        return;
                    }
                }
            }
            consumed = chars.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSpace(this char c)
        {
            for (var i = 0; i < _whitespaceChars.Length; i++)
            {
                if (_whitespaceChars[i] == c)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Decode the span of UTF-16 encoded text represented as base 64 into binary data.
        /// If the input is not a multiple of 4, or contains illegal characters, it will decode as much as it can,
        /// to the largest possible multiple of 4.
        /// This invariant allows continuation of the parse with a slower, whitespace-tolerant algorithm.
        ///
        /// <param name="utf16">
        /// The input span which contains UTF-16 encoded text in base 64 that needs to be decoded.
        /// </param>
        /// <param name="bytes">
        /// The output span which contains the result of the operation, i.e. the decoded binary data.
        /// </param>
        /// <param name="consumed">
        /// The number of input bytes consumed during the operation. This can be used to slice the input for subsequent
        /// calls, if necessary.
        /// </param>
        /// <param name="written">
        /// The number of bytes written into the output span. This can be used to slice the output for subsequent calls,
        /// if necessary.
        /// </param>
        /// <returns>Returns:
        /// - true  - The entire input span was successfully parsed.
        /// - false - Only a part of the input span was successfully parsed. Failure causes may include embedded
        ///           or trailing whitespace, other illegal Base64 characters, trailing characters after an encoding
        ///           pad ('='), an input span whose length is not divisible by 4 or a destination span that's too
        ///           small. <paramref name="consumed"/> and <paramref name="written"/> are set so that  parsing can
        ///           continue with a slower whitespace-tolerant algorithm.
        ///           
        /// Note: This is a cut down version of the implementation of Base64.DecodeFromUtf8(),
        /// modified the accept UTF16 chars and act as a fast-path
        /// helper for the Convert routines when the input string contains no whitespace.
        ///           
        /// </summary> 
        private static bool TryDecodeFromUtf16(
            ReadOnlySpan<char> utf16, Span<byte> bytes, out int consumed, out int written)
        {
            ref char srcChars = ref MemoryMarshal.GetReference(utf16);
            ref byte destBytes = ref MemoryMarshal.GetReference(bytes);

            var srcLength = utf16.Length & ~0x3;  // only decode input up to the closest multiple of 4.
            var destLength = bytes.Length;

            var sourceIndex = 0;
            var destIndex = 0;

            if (utf16.Length == 0)
                goto DoneExit;

            ref sbyte decodingMap = ref _decodingMap[0];

            // Last bytes could have padding characters, so process them separately and treat them as valid.
            const int skipLastChunk = 4;

            int maxSrcLength;
            if (destLength >= (srcLength >> 2) * 3)
            {
                maxSrcLength = srcLength - skipLastChunk;
            }
            else
            {
                // This should never overflow since destLength here is less than int.MaxValue / 4 * 3 (i.e. 1610612733)
                // Therefore, (destLength / 3) * 4 will always be less than 2147483641
                maxSrcLength = destLength / 3 * 4;
            }

            while (sourceIndex < maxSrcLength)
            {
                var result = Decode(ref Unsafe.Add(ref srcChars, sourceIndex), ref decodingMap);
                if (result < 0)
                    goto InvalidExit;
                WriteThreeLowOrderBytes(ref Unsafe.Add(ref destBytes, destIndex), result);
                destIndex += 3;
                sourceIndex += 4;
            }

            if (maxSrcLength != srcLength - skipLastChunk)
                goto InvalidExit;

            // If input is less than 4 bytes, srcLength == sourceIndex == 0
            // If input is not a multiple of 4, sourceIndex == srcLength != 0
            if (sourceIndex == srcLength)
            {
                goto InvalidExit;
            }

            int i0 = Unsafe.Add(ref srcChars, srcLength - 4);
            int i1 = Unsafe.Add(ref srcChars, srcLength - 3);
            int i2 = Unsafe.Add(ref srcChars, srcLength - 2);
            int i3 = Unsafe.Add(ref srcChars, srcLength - 1);
            if (((i0 | i1 | i2 | i3) & 0xffffff00) != 0)
                goto InvalidExit;

            i0 = Unsafe.Add(ref decodingMap, i0);
            i1 = Unsafe.Add(ref decodingMap, i1);

            i0 <<= 18;
            i1 <<= 12;

            i0 |= i1;

            if (i3 != _encodingPad)
            {
                i2 = Unsafe.Add(ref decodingMap, i2);
                i3 = Unsafe.Add(ref decodingMap, i3);

                i2 <<= 6;

                i0 |= i3;
                i0 |= i2;

                if (i0 < 0)
                    goto InvalidExit;
                if (destIndex > destLength - 3)
                    goto InvalidExit;
                WriteThreeLowOrderBytes(ref Unsafe.Add(ref destBytes, destIndex), i0);
                destIndex += 3;
            }
            else if (i2 != _encodingPad)
            {
                i2 = Unsafe.Add(ref decodingMap, i2);

                i2 <<= 6;

                i0 |= i2;

                if (i0 < 0)
                    goto InvalidExit;
                if (destIndex > destLength - 2)
                    goto InvalidExit;
                Unsafe.Add(ref destBytes, destIndex) = (byte)(i0 >> 16);
                Unsafe.Add(ref destBytes, destIndex + 1) = (byte)(i0 >> 8);
                destIndex += 2;
            }
            else
            {
                if (i0 < 0)
                    goto InvalidExit;
                if (destIndex > destLength - 1)
                    goto InvalidExit;
                Unsafe.Add(ref destBytes, destIndex) = (byte)(i0 >> 16);
                destIndex += 1;
            }

            sourceIndex += 4;

            if (srcLength != utf16.Length)
                goto InvalidExit;

DoneExit:
            consumed = sourceIndex;
            written = destIndex;
            return true;

InvalidExit:
            consumed = sourceIndex;
            written = destIndex;
            Debug.Assert(consumed % 4 == 0);
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Decode(ref char encodedChars, ref sbyte decodingMap)
        {
            int i0 = encodedChars;
            int i1 = Unsafe.Add(ref encodedChars, 1);
            int i2 = Unsafe.Add(ref encodedChars, 2);
            int i3 = Unsafe.Add(ref encodedChars, 3);

            if (((i0 | i1 | i2 | i3) & 0xffffff00) != 0)
                return -1; // One or more chars falls outside the 00..ff range. This cannot be a valid Base64 character.

            i0 = Unsafe.Add(ref decodingMap, i0);
            i1 = Unsafe.Add(ref decodingMap, i1);
            i2 = Unsafe.Add(ref decodingMap, i2);
            i3 = Unsafe.Add(ref decodingMap, i3);

            i0 <<= 18;
            i1 <<= 12;
            i2 <<= 6;

            i0 |= i3;
            i1 |= i2;

            i0 |= i1;
            return i0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteThreeLowOrderBytes(ref byte destination, int value)
        {
            destination = (byte)(value >> 16);
            Unsafe.Add(ref destination, 1) = (byte)(value >> 8);
            Unsafe.Add(ref destination, 2) = (byte)value;
        }
    }
}
