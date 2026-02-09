namespace Reluca.Contexts
{
    /// <summary>
    /// 盤の状態を管理します。
    /// 値型（record struct）として定義されており、スタック上にコピーされます。
    /// フィールドは ulong 2 つ（16 バイト）のみであり、値コピーのコストは十分に小さいです。
    /// </summary>
    public record struct BoardContext
    {
        /// <summary>
        /// 黒石の配置状態
        /// </summary>
        public ulong Black { get; set; }

        /// <summary>
        /// 白石の配置状態
        /// </summary>
        public ulong White { get; set; }
    }
}
