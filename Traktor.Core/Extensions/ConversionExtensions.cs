using System;
using System.Collections.Generic;
using System.Text;

namespace Traktor.Core.Extensions
{
    public static class ConversionExtensions
    {
        public static int? ToInt(this string str)
        {
            if (int.TryParse(str, out int result))
                return result;
            return default;
        }

        public static int ToInt(this bool boolean)
        {
            return boolean ? 1 : 0;
        }

        public static long? ToLong(this string str)
        {
            if (long.TryParse(str, out long result))
                return result;
            return default;
        }

        public static long ToUnixTimeSeconds(this DateTime? dt)
        {
            if (dt.HasValue)
                return ((DateTimeOffset)dt.Value).ToUnixTimeSeconds();
            return 0;
        }

        public static long ToUnixTimeSeconds(this DateTime dt)
        {
            return ((DateTimeOffset)dt).ToUnixTimeSeconds();
        }
    }
}
