using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Traktor.Core.Extensions
{
    public static class EnumExtensions
    {
        public static bool Is<T>(this T @enum, params T[] states) where T : Enum
        {
            return states.Any(x => x.Equals(@enum));
        }
    }
}
