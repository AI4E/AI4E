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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AI4E.Utils.Messaging.Primitives
{
    public sealed class MessageBuilder
    {
        private readonly ImmutableList<MessageFrame>.Builder _frames;
        private readonly List<MessageFrameBuilder?> _frameBuilders;

        private int _frameIndex;

        public MessageBuilder()
        {
            _frames = ImmutableList.CreateBuilder<MessageFrame>();
            _frameBuilders = new List<MessageFrameBuilder?>();
            _frameIndex = -1;
        }

        public MessageBuilder(Message message)
        {
            if (message.Frames is ImmutableList<MessageFrame> immutableList)
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

        public MessageBuilder(IEnumerable<MessageFrame> frames)
        {
            if (frames == null)
                throw new ArgumentNullException(nameof(frames));

            _frames = CreateFrameCollection(frames);
            _frameBuilders = CreateFrameBuilderCollection(_frames);
            _frameIndex = _frames.Count - 1;
        }

        private static ImmutableList<MessageFrame>.Builder CreateFrameCollection(IEnumerable<MessageFrame> frames)
        {
            var result = ImmutableList.CreateBuilder<MessageFrame>();
            result.AddRange(frames);
            return result;
        }

        private static List<MessageFrameBuilder?> CreateFrameBuilderCollection(IReadOnlyCollection<MessageFrame> frames)
        {
            var result = new List<MessageFrameBuilder?>(capacity: frames.Count);

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
                    var frameBuilder = _frameBuilders[i];

                    if (frameBuilder is null)
                    {
                        length += _frames[i].Length;
                    }
                    else
                    {
                        length += frameBuilder.Length;
                    }
                }

                length += LengthCodeHelper.Get7BitEndodedIntBytesCount(length);

                return length;
            }
        }

        public MessageFrameBuilder? CurrentFrame
        {
            get
            {
                if (_frameIndex == -1)
                    return null;

                var result = _frameBuilders[_frameIndex];

                if (result is null)
                {
                    result = new MessageFrameBuilder(_frames[_frameIndex]);
                    _frameBuilders[_frameIndex] = result;
                }

                return result;
            }
        }

        public MessageFrameBuilder PushFrame()
        {
            if (_frameIndex == _frames.Count - 1)
            {
                var result = new MessageFrameBuilder();
                _frames.Add(default);
                _frameBuilders.Add(result);
                _frameIndex++;
                return result;
            }

            _frameIndex++;

            Debug.Assert(CurrentFrame != null);
            return CurrentFrame!;
        }

        [return: NotNullIfNotNull("fallback")]
        public MessageFrameBuilder? PopFrame(MessageFrameBuilder? fallback = null)
        {
            if (_frameIndex == -1)
            {
                return fallback;
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

        public Message BuildMessage(bool trim = true)
        {
            for (var i = 0; i < (trim ? _frameIndex + 1 : _frameBuilders.Count); i++)
            {
                var frameBuilder = _frameBuilders[i];

                if (frameBuilder is null)
                    continue;

                _frames[i] = frameBuilder.BuildMessageFrame();
            }

            var frames = _frames.ToImmutable();

            if (trim && _frameIndex != _frames.Count - 1)
            {
                frames = frames.RemoveRange(_frameIndex + 1, _frames.Count - _frameIndex - 1);
            }

            return new Message(frames);
        }
    }
}
