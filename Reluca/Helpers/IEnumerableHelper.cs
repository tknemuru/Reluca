using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Helpers
{
    /// <summary>
    /// IEnumerableに関する補助機能を提供します。
    /// </summary>
    public static class IEnumerableHelper
    {
        /// <summary>
        /// IEnumerable を文字列に変換します。
        /// </summary>
        /// <returns>The numerable to string.</returns>
        /// <param name="strs">Strs.</param>
        public static string IEnumerableToString(IEnumerable<string> strs)
        {
            var sb = new StringBuilder();
            foreach (var str in strs)
            {
                sb.AppendLine(str);
            }
            return sb.ToString();
        }

        /// <summary>
        /// IEnumerable に変換した enum を取得します。
        /// </summary>
        /// <returns>The enums.</returns>
        /// <param name="enm">Enm.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        public static IEnumerable<T> GetEnums<T>() where T : struct
        {
            return (IEnumerable<T>)Enum.GetValues(typeof(T));
        }
    }
}
