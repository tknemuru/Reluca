/// <summary>
/// 【ModuleDoc】
/// 責務: テストアセンブリレベルの設定を定義する
/// 入出力: なし
/// 副作用: MSTest のテスト並列実行をクラスレベルで有効化する
/// </summary>
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.ClassLevel)]
