using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Helpers
{
    /// <summary>
    /// ファイル操作に関する補助機能を提供します。
    /// </summary>
    public static class FileHelper
    {
        /// <summary>
        /// エンコードの初期値
        /// </summary>
        private const string DefaultEncoding = "UTF-8";

        /// <summary>
        /// <para>ファイルから文字列のリストを取得します。</para>
        /// </summary>
        /// <param name="filePath">ファイルパス</param>
        /// <returns>文字列のリスト</returns>
        public static IEnumerable<string> ReadTextLines(string filePath, Encoding? encoding = null)
        {
            if (encoding == null) { encoding = Encoding.GetEncoding(DefaultEncoding); }

            string line;
            using (StreamReader sr = new StreamReader(filePath, encoding))
            {
#pragma warning disable CS8600 // Null リテラルまたは Null の可能性がある値を Null 非許容型に変換しています。
                while ((line = sr.ReadLine()) != null)
                {
                    yield return line;
                }
#pragma warning restore CS8600 // Null リテラルまたは Null の可能性がある値を Null 非許容型に変換しています。
            }
        }
    }
}
