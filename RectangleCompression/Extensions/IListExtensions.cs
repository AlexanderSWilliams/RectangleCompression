using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RectangleCompression.IListExtensions
{
    public static class IListExtensions
    {
        public static List<T> SubList<T>(this IList<T> data, int index, int length)
        {
            var result = new List<T>();
            for (int i = 0; i < length; i++)
            {
                result.Add(data[index + i]);
            }
            return result;
        }

        public static List<T> SubList<T>(this IList<T> data, int index)
        {
            var result = new List<T>();
            for (int i = 0, length = data.Count - index; i < length; i++)
            {
                result.Add(data[index + i]);
            }
            return result;
        }
    }
}