using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Models
{
    public static class Board
    {
        /// <summary>
        /// 状態
        /// </summary>
        public enum Status
        {
            /// <summary>
            /// 空
            /// </summary>
            Empty,
            /// <summary>
            /// 配置可能
            /// </summary>
            Mobility,
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
        /// 状態を示すアイコン
        /// </summary>
        public static class Icon
        {
            /// <summary>
            /// 空
            /// </summary>
            public const char Empty = '　';

            /// <summary>
            /// 配置可能
            /// </summary>
            public const char Mobility = '・';

            /// <summary>
            /// 黒石
            /// </summary>
            public const char Black = '●';

            /// <summary>
            /// 白石
            /// </summary>
            public const char White = '○';
        };

        /// <summary>
        /// 盤の一辺の長さ
        /// </summary>
        public const int Length = 8;

        /// <summary>
        /// 盤の全マス目数
        /// </summary>
        public const int AllLength = 64;
    }
}
