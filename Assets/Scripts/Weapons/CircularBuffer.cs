using System;

namespace Weapons
{
    public class CircularBuffer
    {
        private readonly float[] _buffer;
        private int _head;
        private int _tail;
        private int _count;

        public CircularBuffer(int capacity)
        {
            _buffer = new float[capacity];
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        public int Count => _count;

        public void Enqueue(float value)
        {
            _buffer[_tail] = value;
            _tail = (_tail + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
            else
                _head = (_head + 1) % _buffer.Length;
        }

        public float Peek()
        {
            return _buffer[_head];
        }

        public float Dequeue()
        {
            float value = _buffer[_head];
            _head = (_head + 1) % _buffer.Length;
            _count--;
            return value;
        }

        public void Clear()
        {
            _head = 0;
            _tail = 0;
            _count = 0;
        }
    }
}
