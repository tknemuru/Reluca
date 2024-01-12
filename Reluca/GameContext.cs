using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca
{
    /// <summary>
    /// ゲームの状態を管理します。
    /// </summary>
    public record GameContext
    {
        /// <summary>
        /// 盤状態
        /// </summary>
        public BoardContext BoardContext { get; set; }

        /// <summary>
        /// 黒石の配置状態
        /// </summary>
        public ulong Black
        {
            get { return BoardContext.Black; }
            set { BoardContext.Black = value; }
        }

        /// <summary>
        /// 白石の配置状態
        /// </summary>
        public ulong White {
            get { return BoardContext.White; }
            set { BoardContext.White = value; }
        }

        /// <summary>
        /// 配置可能状態
        /// </summary>
        public ulong Mobility { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public GameContext()
        {
            BoardContext = new BoardContext();
        }
    }
}
