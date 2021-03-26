// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using System.Runtime.CompilerServices;

namespace Common.Extensions
{
    public static class EnumExtensions
    {
        public static T ToEnum<T>(this string str) where T : struct
        {
            T value;
            if (!Enum.TryParse<T>(str, out value))
                return default;

            return value;
        }

        public static TInt AsInteger<TEnum, TInt>(this TEnum enumValue)
            where TEnum : unmanaged, Enum
            where TInt : unmanaged
        {
            if (Unsafe.SizeOf<TEnum>() != Unsafe.SizeOf<TInt>()) throw new Exception("type mismatch");
            TInt value = Unsafe.As<TEnum, TInt>(ref enumValue);
            return value;
        }
    }
}
