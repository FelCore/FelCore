// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System.Threading;

namespace Common
{
    /// <summary>
    /// Interlocked support for boolean values
    /// </summary>
    public struct InterlockedBoolean
    {
        private int _value;

        /// <summary>
        /// Current value
        /// </summary>
        public bool Value
        {
            get { return _value == 1; }
            set { _value = value ? 1 : 0; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="initialValue">initial value</param>
        public InterlockedBoolean(bool initialValue = false)
        {
            _value = initialValue ? 1 : 0;
        }

        /// <summary>
        /// Sets a new value
        /// </summary>
        /// <param name="newValue">new value</param>
        /// <returns>the original value before any operation was performed</returns>
        public bool Exchange(bool newValue)
        {
            var oldValue = Interlocked.Exchange(ref _value, newValue ? 1 : 0);
            return oldValue == 1;
        }

        /// <summary>
        /// Compares the current value and the comparand for equality and, if they are equal, 
        /// replaces the current value with the new value in an atomic/thread-safe operation.
        /// </summary>
        /// <param name="newValue">new value</param>
        /// <param name="comparand">value to compare the current value with</param>
        /// <returns>the original value before any operation was performed</returns>
        public bool CompareExchange(bool newValue, bool comparand)
        {
            var oldValue = Interlocked.CompareExchange(ref _value, newValue ? 1 : 0, comparand ? 1 : 0);
            return oldValue == 1;
        }
        public static implicit operator bool(InterlockedBoolean obj)
        {
            return obj._value == 1;
        }
    }
}
