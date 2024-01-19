using Reluca.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Contexts
{
    /// <summary>
    /// ゲームの状態を管理します。
    /// </summary>
    public record GameContext
    {
        /// <summary>
        /// 盤状態
        /// </summary>
        public BoardContext Board { get; set; }

        /// <summary>
        /// 黒石の配置状態
        /// </summary>
        public ulong Black
        {
            get { return Board.Black; }
            set { Board.Black = value; }
        }

        /// <summary>
        /// 白石の配置状態
        /// </summary>
        public ulong White
        {
            get { return Board.White; }
            set { Board.White = value; }
        }

        /// <summary>
        /// 配置可能状態
        /// </summary>
        public ulong Mobility { get; set; }

        /// <summary>
        /// ターン
        /// </summary>
        public Disc.Color Turn { get; set; }

        /// <summary>
        /// 指し手
        /// </summary>
        public int Move { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public GameContext()
        {
            Board = new BoardContext();
            Turn = Disc.Color.Undefined;
            Move = -1;
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="boardContext">盤状態</param>
        public GameContext(BoardContext boardContext)
        {
            Board = boardContext;
        }
    }
}
