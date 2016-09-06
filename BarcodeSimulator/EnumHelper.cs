using System;
using System.Collections.Generic;

namespace BarcodeSimulator
{
    public static class EnumHelper
    {
        /// <summary>
        /// Gets all items for an enum type.
        /// </summary>
        public static IEnumerable<T> GetAllItems<T>() where T : struct
        {
            foreach (object item in Enum.GetValues(typeof(T)))
            {
                yield return (T)item;
            }
        }

    }
}
