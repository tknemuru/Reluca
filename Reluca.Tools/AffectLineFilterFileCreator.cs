using Reluca.Accessors;
using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Tools
{
    /// <summary>
    /// 影響ラインフィルタで利用するファイルの作成機能を提供します。
    /// </summary>
    public class AffectLineFilterFileCreator
    {
        public void Create()
        {
            for (int i = 0; i < Board.AllLength; i++)
            {
                Create(i);
            }
        }

        /// <summary>
        /// 指し手の位置をもとに着手可能
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public List<ulong> Create(int i)
        {
            var results = new List<ulong>();
            ulong result = 0;
            var index = i;
            int startLine = i / Board.Length;
            int currentLine = startLine;
            // 右
            while (currentLine == startLine)
            {
                result |= 1ul << index;
                index++;
                currentLine = index / Board.Length;
            }
            // 左
            index = i;
            currentLine = startLine;
            while (currentLine == startLine)
            {
                result |= 1ul << index;
                index--;
                currentLine = index / Board.Length;
            }
            results.Add(result);

            // 上
            result = 0;
            index = i;
            while (index >= 0)
            {
                result |= 1ul << index;
                index -= Board.Length;
            }
            // 下
            index = i;
            while (index < Board.AllLength)
            {
                result |= 1ul << index;
                index += Board.Length;
            }
            results.Add(result);

            // 右上
            result = 0;
            index = i;
            while (index >= 0)
            {
                result |= 1ul << index;
                index++;
                index -= Board.Length;
            }
            // 左下
            index = i;
            var orgColIndex = BoardAccessor.GetColumnIndex(index);
            while (index < Board.AllLength && BoardAccessor.GetColumnIndex(index) <= orgColIndex)
            {
                result |= 1ul << index;
                index--;
                index += Board.Length;
            }
            if (result != 1ul << index)
            {
                results.Add(result);
            }

            // 左上
            result = 0;
            index = i;
            while (index >= 0)
            {
                result |= 1ul << index;
                index--;
                index -= Board.Length;
            }
            // 右下
            index = i;
            orgColIndex = BoardAccessor.GetColumnIndex(index);
            while (index < Board.AllLength && BoardAccessor.GetColumnIndex(index) >= orgColIndex)
            {
                result |= 1ul << index;
                index++;
                index += Board.Length;
            }
            if (result != 1ul << index)
            {
                results.Add(result);
            }

            return results;
        }
    }
}
