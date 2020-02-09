using System;
using System.Runtime.CompilerServices;

namespace RainDropEffect2.Scripts.Common
{
    public sealed class StackPool<T>
    {
        private readonly int _initialCapacity;
        private int _freeIndex;
        private T[] _storage;
        private Func<T> _factoryMethod;

        public StackPool(int initialCapacity, Func<T> factoryMethod = null, bool preWarm = false)
        {
            _initialCapacity = initialCapacity;
            _factoryMethod = factoryMethod;
            _storage = new T[_initialCapacity];

            if (preWarm)
                PreWarm = _initialCapacity;
        }

        public bool IsEmpty => _freeIndex == 0;
        public int Available => _freeIndex;
        public int Size => _storage.Length;

        public Func<T> FactoryMethod
        {
            set => _factoryMethod = value;
        }

        public T Get()
        {
#if DEBUG
            if (IsEmpty && _factoryMethod == null)
                throw new Exception(
                    "Pool is empty and 'factory method' isn't set, so method 'Get' can't create and return a new instance!");
#endif

            return _freeIndex == 0 ? _factoryMethod() : _storage[--_freeIndex];
        }

        public void Put(T item)
        {
            if (_freeIndex == _storage.Length)
                _ResizePool();

            _storage[_freeIndex++] = item;
        }

        public int PreWarm
        {
            set
            {
#if DEBUG
                if (_factoryMethod == null)
                    throw new Exception("Can't use PreWork since 'FactoryMethod' isn't defined!");
#endif
                if (value > Available) _ResizePool();

                for (var i = _freeIndex; i < _storage.Length; i++)
                {
                    Put(_factoryMethod());
                }
            }
        }

        public void Clear()
        {
            _freeIndex = 0;
            _storage = new T[_initialCapacity];
        }

        public void Dispose()
        {
            _storage = null;
            _factoryMethod = null;
        }

        public override string ToString()
        {
            return $"Size: {Size}, Available: {Available}";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _ResizePool()
        {
            var count = _storage.Length == 0 ? 2 : _storage.Length * 2;
            var newOne = new T[count];
            Array.Copy(_storage, newOne, _storage.Length);
            _storage = newOne;
        }
    }
}