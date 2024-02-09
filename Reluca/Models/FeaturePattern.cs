using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Reluca.Models
{
    /// <summary>
    /// 特徴パターン
    /// </summary>
    public static class FeaturePattern
    {
        /// <summary>
        /// 種類と名称の変換辞書
        /// </summary>
        private static readonly Dictionary<string, Type> TypeNameDic = new Dictionary<string, Type>()
        {
            [TypeName.Diag4] = Type.Diag4,
            [TypeName.Diag5] = Type.Diag5,
            [TypeName.Diag6] = Type.Diag6,
            [TypeName.Diag7] = Type.Diag7,
            [TypeName.Diag8] = Type.Diag8,
            [TypeName.HorVert2] = Type.HorVert2,
            [TypeName.HorVert3] = Type.HorVert3,
            [TypeName.HorVert4] = Type.HorVert4,
            [TypeName.Edge2X] = Type.Edge2X,
            [TypeName.Corner2X5] = Type.Corner2X5,
            [TypeName.Corner3X3] = Type.Corner3X3,
        };

        /// <summary>
        /// 種類と桁数の変換辞書
        /// </summary>
        private static readonly Dictionary<Type, ushort> TypeDigitDic = new Dictionary<Type, ushort>()
        {
            [Type.Diag4] = 4,
            [Type.Diag5] = 5,
            [Type.Diag6] = 6,
            [Type.Diag7] = 7,
            [Type.Diag8] = 8,
            [Type.HorVert2] = 8,
            [Type.HorVert3] = 8,
            [Type.HorVert4] = 8,
            [Type.Edge2X] = 10,
            [Type.Corner2X5] = 10,
            [Type.Corner3X3] = 9,
        };

        /// <summary>
        /// 種類
        /// </summary>
        public enum Type
        {
            Diag4,
            Diag5,
            Diag6,
            Diag7,
            Diag8,
            HorVert2,
            HorVert3,
            HorVert4,
            Edge2X,
            Corner2X5,
            Corner3X3,
        }

        /// <summary>
        /// 種類名称
        /// </summary>
        public static class TypeName
        {
            public const string Diag4 = "diag4";
            public const string Diag5 = "diag5";
            public const string Diag6 = "diag6";
            public const string Diag7 = "diag7";
            public const string Diag8 = "diag8";
            public const string HorVert2 = "hor_vert2";
            public const string HorVert3 = "hor_vert3";
            public const string HorVert4 = "hor_vert4";
            public const string Edge2X = "edge2X";
            public const string Corner2X5 = "corner2X5";
            public const string Corner3X3 = "corner3X3";
        }

        /// <summary>
        /// 種類の桁数
        /// </summary>
        public static class TypeDigit
        {
            /// <summary>
            /// 最小
            /// </summary>
            public const ushort Min = 4;

            /// <summary>
            /// 最大
            /// </summary>
            public const ushort Max = 10;
        }

        /// <summary>
        /// 盤状態の連番
        /// </summary>
        public static class BoardStateSequence
        {
            /// <summary>
            /// 白
            /// </summary>
            public const ushort White = 0;
            /// <summary>
            /// 空
            /// </summary>
            public const ushort Empty = 1;
            /// <summary>
            /// 黒
            /// </summary>
            public const ushort Black = 2;
        }

        /// <summary>
        /// 名称から種類を取得します。
        /// </summary>
        /// <param name="name">種類名称</param>
        /// <returns>種類</returns>
        public static Type GetType(string name)
        {
            return TypeNameDic[name];
        }

        /// <summary>
        /// 指定した種類の桁数を取得します。
        /// </summary>
        /// <param name="type">種類</param>
        /// <returns>種類の桁数</returns>
        public static ushort GetDigit(Type type)
        {
            return TypeDigitDic[type];
        }
    }
}
