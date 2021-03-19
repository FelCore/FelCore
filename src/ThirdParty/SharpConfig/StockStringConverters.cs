using System;
using System.ComponentModel;
using System.Globalization;

namespace SharpConfig
{
  internal sealed class FallbackStringConverter : ITypeStringConverter
  {
    public string ConvertToString(object value)
    {
      try
      {
        var converter = TypeDescriptor.GetConverter(value);
        return converter.ConvertToString(null, Configuration.CultureInfo, value);
      }
      catch (Exception ex)
      {
        throw SettingValueCastException.Create(value.ToString(), value.GetType(), ex);
      }
    }

    public object ConvertFromString(string value, Type hint)
    {
      try
      {
        var converter = TypeDescriptor.GetConverter(hint);
        return converter.ConvertFrom(null, Configuration.CultureInfo, value);
      }
      catch (Exception ex)
      {
        throw SettingValueCastException.Create(value, hint, ex);
      }
    }

    public Type ConvertibleType => null;

    public object TryConvertFromString(string value, Type hint)
    {
      // Just call ConvertFromString since implementation is already in a try-catch block.
      return ConvertFromString(value, hint);
    }
  }

  internal sealed class BoolStringConverter : TypeStringConverter<bool>
  {
    public override string ConvertToString(object value)
      => value.ToString();

    public override object TryConvertFromString(string value, Type hint)
    {
      switch (value.ToLowerInvariant())
      {
        case "":
        case "false":
        case "off":
        case "no":
        case "n":
        case "0":
          return false;
        case "true":
        case "on":
        case "yes":
        case "y":
        case "1":
          return true;
        default:
          return null;
      }
    }
  }

  internal sealed class ByteStringConverter : TypeStringConverter<byte>
  {
    public override string ConvertToString(object value)
      => value.ToString();

    public override object TryConvertFromString(string value, Type hint)
    {
      if (value == string.Empty)
        return default(byte);

      if (!byte.TryParse(value, NumberStyles.Integer, Configuration.CultureInfo.NumberFormat, out var result))
        return null;

      return result;
    }
  }

  internal sealed class CharStringConverter : TypeStringConverter<char>
  {
    public override string ConvertToString(object value)
      => value.ToString();

    public override object TryConvertFromString(string value, Type hint)
    {
      if (value == string.Empty)
        return default(char);

      if (!char.TryParse(value, out var result))
        return null;

      return result;
    }
  }

  internal sealed class DateTimeStringConverter : TypeStringConverter<DateTime>
  {
    public override string ConvertToString(object value)
      => ((DateTime)value).ToString(Configuration.CultureInfo.DateTimeFormat);

    public override object TryConvertFromString(string value, Type hint)
    {
      if (value == string.Empty)
        return default(DateTime);

      if (!DateTime.TryParse(value, Configuration.CultureInfo.DateTimeFormat, DateTimeStyles.None, out var result))
        return null;

      return result;
    }
  }

  internal sealed class DecimalStringConverter : TypeStringConverter<decimal>
  {
    public override string ConvertToString(object value)
      => ((decimal)value).ToString(Configuration.CultureInfo.NumberFormat);

    public override object TryConvertFromString(string value, Type hint)
    {
      if (value == string.Empty)
        return default(decimal);

      if (!decimal.TryParse(value, NumberStyles.Number, Configuration.CultureInfo.NumberFormat, out var result))
        return null;

      return result;
    }
  }

  internal sealed class DoubleStringConverter : TypeStringConverter<double>
  {
    public override string ConvertToString(object value)
      => ((double)value).ToString(Configuration.CultureInfo.NumberFormat);

    public override object TryConvertFromString(string value, Type hint)
    {
      if (value == string.Empty)
        return default(double);

      if (!double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, Configuration.CultureInfo.NumberFormat, out var result))
        return null;

      return result;
    }
  }

  internal sealed class EnumStringConverter : TypeStringConverter<Enum>
  {
    public override string ConvertToString(object value)
      => value.ToString();

    public override object TryConvertFromString(string value, Type hint)
    {
      if (value == string.Empty)
        return default(double);

      value = RemoveTypeNames(value);
      return Enum.IsDefined(hint, value) ? Enum.Parse(hint, value) : null;
    }

    /// <summary>
    /// Removes possible type names from a string value.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <remarks>
    /// It's possible that the value is something like:
    /// UriFormat.Unescaped
    /// We, and especially Enum.Parse do not want this format. Instead, it wants the clean name like:
    /// Unescaped
    /// </remarks>
    private static string RemoveTypeNames(string value)
    {
      var indexOfLastDot = value.LastIndexOf('.');
      if (indexOfLastDot >= 0)
        value = value.Substring(indexOfLastDot + 1, value.Length - indexOfLastDot - 1).Trim();
      return value;
    }
  }

  internal sealed class Int16StringConverter : TypeStringConverter<short>
  {
    public override string ConvertToString(object value)
      => ((short)value).ToString(Configuration.CultureInfo.NumberFormat);

    public override object TryConvertFromString(string value, Type hint)
    {
      if (value == string.Empty)
        return default(short);

      if (!short.TryParse(value, NumberStyles.Integer, Configuration.CultureInfo.NumberFormat, out var result))
        return null;

      return result;
    }
  }

  internal sealed class Int32StringConverter : TypeStringConverter<int>
  {
    public override string ConvertToString(object value)
      => ((int)value).ToString(Configuration.CultureInfo.NumberFormat);

    public override object TryConvertFromString(string value, Type hint)
    {
      if (value == string.Empty)
        return default(int);

      if (!int.TryParse(value, NumberStyles.Integer, Configuration.CultureInfo.NumberFormat, out var result))
        return null;

      return result;
    }
  }

  internal sealed class Int64StringConverter : TypeStringConverter<long>
  {
    public override string ConvertToString(object value)
      => ((long)value).ToString(Configuration.CultureInfo.NumberFormat);

    public override object TryConvertFromString(string value, Type hint)
    {
      if (value == string.Empty)
        return default(long);

      if (!long.TryParse(value, NumberStyles.Integer, Configuration.CultureInfo.NumberFormat, out var result))
        return null;

      return result;
    }
  }

  internal sealed class SByteStringConverter : TypeStringConverter<sbyte>
  {
    public override string ConvertToString(object value)
      => ((sbyte)value).ToString(Configuration.CultureInfo.NumberFormat);

    public override object TryConvertFromString(string value, Type hint)
    {
      if (value == string.Empty)
        return default(sbyte);

      if (!sbyte.TryParse(value, NumberStyles.Integer, Configuration.CultureInfo.NumberFormat, out var result))
        return null;

      return result;
    }
  }

  internal sealed class SingleStringConverter : TypeStringConverter<float>
  {
    public override string ConvertToString(object value)
      => ((float)value).ToString(Configuration.CultureInfo.NumberFormat);

    public override object TryConvertFromString(string value, Type hint)
    {
      if (value == string.Empty)
        return default(float);

      if (!float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, Configuration.CultureInfo.NumberFormat, out var result))
        return null;

      return result;
    }
  }

  internal sealed class StringStringConverter : TypeStringConverter<string>
  {
    public override string ConvertToString(object value)
      => value.ToString().Trim('\"');

    public override object TryConvertFromString(string value, Type hint)
      => value.Trim('\"');
  }

  internal sealed class UInt16StringConverter : TypeStringConverter<ushort>
  {
    public override string ConvertToString(object value)
      => ((ushort)value).ToString(Configuration.CultureInfo.NumberFormat);

    public override object TryConvertFromString(string value, Type hint)
    {
      if (value == string.Empty)
        return default(ushort);

      if (!ushort.TryParse(value, NumberStyles.Integer, Configuration.CultureInfo.NumberFormat, out var result))
        return null;

      return result;
    }
  }

  internal sealed class UInt32StringConverter : TypeStringConverter<uint>
  {
    public override string ConvertToString(object value)
      => ((uint)value).ToString(Configuration.CultureInfo.NumberFormat);

    public override object TryConvertFromString(string value, Type hint)
    {
      if (value == string.Empty)
        return default(uint);

      if (!uint.TryParse(value, NumberStyles.Integer, Configuration.CultureInfo.NumberFormat, out var result))
        return null;

      return result;
    }
  }

  internal sealed class UInt64StringConverter : TypeStringConverter<ulong>
  {
    public override string ConvertToString(object value)
      => ((ulong)value).ToString(Configuration.CultureInfo.NumberFormat);

    public override object TryConvertFromString(string value, Type hint)
    {
      if (value == string.Empty)
        return default(ulong);

      if (!ulong.TryParse(value, NumberStyles.Integer, Configuration.CultureInfo.NumberFormat, out var result))
        return null;

      return result;
    }
  }
}
