using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Models
{
    /// <summary>
    /// 標準テキストファイル
    /// </summary>
    public static class SimpleText
    {
        /// <summary>
        /// キーと値を分離する文字
        /// </summary>
        public const char KeyValueSeparator = ':';

        /// <summary>
        /// 値を分離する文字
        /// </summary>
        public const char ValueSeparator = '|';

        /// <summary>
        /// コンテキストを分離する文字
        /// </summary>
        public const string ContextSeparator = "-";

        /// <summary>
        /// キー
        /// </summary>
        public static class Key
        {
            /// <summary>
            /// ターン
            /// </summary>
            public const string Turn = "ターン";

            /// <summary>
            /// 指し手
            /// </summary>
            public const string Move = "指し手";

            /// <summary>
            /// 盤
            /// </summary>
            public const string Board = "盤";
        }
    }
}
