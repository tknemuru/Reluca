using Reluca.Models;

namespace Reluca.Contexts
{
    /// <summary>
    /// ゲームの状態を管理します。
    /// BoardContext が record struct（値型）であるため、Black/White のセッターは
    /// Board プロパティ全体を再代入する方式で実装しています。
    /// </summary>
    public record GameContext
    {
        /// <summary>
        /// 盤状態
        /// </summary>
        public BoardContext Board { get; set; }

        /// <summary>
        /// 黒石の配置状態。
        /// Board が値型のため、セッターでは Board 全体を再代入します。
        /// </summary>
        public ulong Black
        {
            get { return Board.Black; }
            set { Board = Board with { Black = value }; }
        }

        /// <summary>
        /// 白石の配置状態。
        /// Board が値型のため、セッターでは Board 全体を再代入します。
        /// </summary>
        public ulong White
        {
            get { return Board.White; }
            set { Board = Board with { White = value }; }
        }

        /// <summary>
        /// 配置可能状態
        /// </summary>
        public ulong Mobility { get; set; }

        /// <summary>
        /// ターン数
        /// </summary>
        public int TurnCount { get; set; }

        /// <summary>
        /// ステージ
        /// </summary>
        public int Stage { get; set; }

        /// <summary>
        /// ターン
        /// </summary>
        public Disc.Color Turn { get; set; }

        /// <summary>
        /// 指し手
        /// </summary>
        public int Move { get; set; }

        /// <summary>
        /// コンストラクタ。デフォルト値で初期化します。
        /// </summary>
        public GameContext()
        {
            TurnCount = -1;
            Stage = -1;
            Board = new BoardContext();
            Turn = Disc.Color.Undefined;
            Move = -1;
        }

        /// <summary>
        /// コンストラクタ。指定した盤状態で初期化します。
        /// </summary>
        /// <param name="boardContext">盤状態</param>
        public GameContext(BoardContext boardContext)
        {
            Board = boardContext;
        }
    }
}
