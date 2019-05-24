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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using AI4E.Internal;

namespace AI4E.Remoting
{
    public sealed class ValueMessageBuilder
    {
        private readonly ImmutableList<ValueMessageFrame>.Builder _frames;
        private readonly List<ValueMessageFrameBuilder> _frameBuilders;

        private int _frameIndex;

        public ValueMessageBuilder()
        {
            _frames = ImmutableList.CreateBuilder<ValueMessageFrame>();
            _frameBuilders = new List<ValueMessageFrameBuilder>();
            _frameIndex = -1;
        }

        public ValueMessageBuilder(ValueMessage message)
        {
            if (message.Frames is ImmutableList<ValueMessageFrame> immutableList)
            {
                _frames = immutableList.ToBuilder();
            }
            else
            {
                _frames = CreateFrameCollection(message.Frames);
            }

            _frameBuilders = CreateFrameBuilderCollection(_frames);
            _frameIndex = _frames.Count - 1;
        }

        public ValueMessageBuilder(IEnumerable<ValueMessageFrame> frames)
        {
            if (frames == null)
                throw new ArgumentNullException(nameof(frames));

            _frames = CreateFrameCollection(frames);
            _frameBuilders = CreateFrameBuilderCollection(_frames);
            _frameIndex = _frames.Count - 1;
        }

        private static ImmutableList<ValueMessageFrame>.Builder CreateFrameCollection(IEnumerable<ValueMessageFrame> frames)
        {
            var result = ImmutableList.CreateBuilder<ValueMessageFrame>();
            result.AddRange(frames);
            return result;
        }

        private static List<ValueMessageFrameBuilder> CreateFrameBuilderCollection(IReadOnlyCollection<ValueMessageFrame> frames)
        {
            var result = new List<ValueMessageFrameBuilder>(capacity: frames.Count);

            for (var i = 0; i < frames.Count; i++)
                result.Add(null);

            return result;
        }

        public int Length
        {
            get
            {
                var length = 0;

                for (var i = 0; i < _frames.Count; i++)
                {
                    if (_frameBuilders[i] is null)
                    {
                        length += _frames[i].Length;
                    }
                    else
                    {
                        length += _frameBuilders[i].Length;
                    }
                }

                length += LengthCodeHelper.Get7BitEndodedIntBytesCount(length);

                return length;
            }
        }

        public ValueMessageFrameBuilder CurrentFrame
        {
            get
            {
                if (_frameIndex == -1)
                    return null;

                var result = _frameBuilders[_frameIndex];

                if (result is null)
                {
                    result = new ValueMessageFrameBuilder(_frames[_frameIndex]);
                    _frameBuilders[_frameIndex] = result;
                }

                return result;
            }
        }

        public ValueMessageFrameBuilder PushFrame()
        {
            if (_frameIndex == _frames.Count - 1)
            {
                var result = new ValueMessageFrameBuilder();
                _frames.Add(default);
                _frameBuilders.Add(result);
                _frameIndex++;
                return result;
            }

            _frameIndex++;
            return CurrentFrame;
        }

        public ValueMessageFrameBuilder PopFrame()
        {
            if (_frameIndex == -1)
            {
                return null;
            }

            var result = CurrentFrame;
            _frameIndex--;
            return result;
        }

        public void Trim()
        {
            while (_frameIndex < _frames.Count - 1)
            {
                _frames.RemoveAt(_frames.Count - 1);
                _frameBuilders.RemoveAt(_frameBuilders.Count - 1);
            }
        }

        public void Clear()
        {
            _frameIndex = -1;
            _frames.Clear();
            _frameBuilders.Clear();
        }

        public ValueMessage BuildMessage(bool trim = true)
        {
            for (var i = 0; i < (trim ? _frameIndex + 1 : _frameBuilders.Count); i++)
            {
                if (_frameBuilders[i] is null)
                    continue;

                _frames[i] = _frameBuilders[i].BuildMessageFrame();
            }

            var frames = _frames.ToImmutable();

            if (trim && _frameIndex != _frames.Count - 1)
            {
                frames = frames.RemoveRange(_frameIndex + 1, _frames.Count - _frameIndex - 1);
            }

            return new ValueMessage(frames);
        }
    }
}
