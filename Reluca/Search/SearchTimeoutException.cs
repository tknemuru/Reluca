/// <summary>
/// 【ModuleDoc】
/// 責務: 探索の制限時間超過時にスローされる例外を定義する
/// 入出力: なし
/// 副作用: なし
///
/// PVS 探索の再帰呼び出しを一括で中断するために使用する。
/// 本例外は PvsSearchEngine.Search() 内の try-catch で必ず捕捉される。
/// Search() の外部には伝播しない設計であり、ISearchEngine の利用者が
/// 本例外を処理する必要はない。コードレビュー時は Search() 内の
/// catch (SearchTimeoutException) が維持されていることを確認すること。
/// </summary>
namespace Reluca.Search
{
    /// <summary>
    /// 探索の制限時間超過時にスローされる例外。
    /// PVS 探索の再帰呼び出しを一括で中断するために使用する。
    /// 本例外は PvsSearchEngine.Search() 内の try-catch で必ず捕捉される。
    /// Search() の外部には伝播しない設計であり、ISearchEngine の利用者が
    /// 本例外を処理する必要はない。
    /// </summary>
    public class SearchTimeoutException : Exception
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public SearchTimeoutException()
            : base("探索の制限時間を超過しました。")
        {
        }
    }
}
