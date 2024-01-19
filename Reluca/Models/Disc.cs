using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Models
{
    /// <summary>
    /// 石
    /// </summary>
    public static class Disc
    {
        /// <summary>
        /// 色
        /// </summary>
        public enum Color
        {
            /// <summary>
            /// 不確定
            /// </summary>
            Undefined,

            /// <summary>
            /// 黒
            /// </summary>
            Black,

            /// <summary>
            /// 白
            /// </summary>
            White
        }

        /// <summary>
        /// 色名
        /// </summary>
        public static class ColorName
        {
            /// <summary>
            /// 黒
            /// </summary>
            public const string Black = "黒";

            /// <summary>
            /// 白
            /// </summary>
            public const string White = "白";
        }
    }
}
