/// <summary>
/// 【ModuleDoc】
/// 責務: アプリケーション全体の依存性注入コンテナを管理する
/// 入出力: なし（静的プロバイダ）
/// 副作用: 初回アクセス時にサービスコンテナを構築
/// </summary>
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reluca.Analyzers;
using Reluca.Cachers;
using Reluca.Contexts;
using Reluca.Converters;
using Reluca.Evaluates;
using Reluca.Movers;
using Reluca.Search;
using Reluca.Search.Transposition;
using Reluca.Serchers;
using Reluca.Services;
using Reluca.Updaters;
using Serilog;
using Serilog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Di
{
    /// <summary>
    /// DIの生成機能を提供します。
    /// </summary>
    public static class DiProvider
    {
        /// <summary>
        /// デフォルトサービスプロバイダ
        /// </summary>
        private static ServiceProvider DefaultProvider { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        static DiProvider()
        {
            DefaultProvider = BuildDefaultProvider();
        }

        /// <summary>
        /// デフォルトサービスプロバイダを取得します。
        /// </summary>
        /// <returns>デフォルトサービスプロバイダ</returns>
        public static ServiceProvider Get()
        {
            return DefaultProvider;
        }

        /// <summary>
        /// デフォルトのサービスプロバイダを組み立てます。
        /// </summary>
        /// <returns>デフォルトのサービスプロバイダ</returns>
        private static ServiceProvider BuildDefaultProvider()
        {
            // Serilog 構成（DI コンテナ内で完結させ、グローバル静的ロガーへの代入は行わない）
            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}")
                .WriteTo.File(
                    new Serilog.Formatting.Json.JsonFormatter(),
                    "./log/structured/reluca-.json",
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 10_000_000,
                    retainedFileCountLimit: 30,
                    rollOnFileSizeLimit: true)
                .CreateLogger();

            var services = new ServiceCollection();

            // Microsoft.Extensions.Logging 統合
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(serilogLogger, dispose: true);
            });
            services.AddSingleton<StringToBoardContextConverter, StringToBoardContextConverter>();
            services.AddSingleton<BoardContextToStringConverter, BoardContextToStringConverter>();
            services.AddSingleton<StringToMobilityBoardConverter, StringToMobilityBoardConverter>();
            services.AddSingleton<MobilityBoardToStringConverter, MobilityBoardToStringConverter>();
            services.AddSingleton<StringToGameContextConverter, StringToGameContextConverter>();
            services.AddSingleton<GameContextToStringConverter, GameContextToStringConverter>();
            services.AddSingleton<MoveAndReverseUpdater, MoveAndReverseUpdater>();
            services.AddSingleton<MobilityUpdater, MobilityUpdater>();
            services.AddSingleton<InitializeUpdater, InitializeUpdater>();
            services.AddSingleton<GameEndJudge, GameEndJudge>();
            services.AddSingleton<FindFirstMover, FindFirstMover>();
            services.AddSingleton<FeaturePatternExtractor, FeaturePatternExtractor>();
            services.AddSingleton<FeaturePatternNormalizer, FeaturePatternNormalizer>();
            services.AddSingleton<NoneNormalizer, NoneNormalizer>();
            services.AddSingleton<FeaturePatternEvaluator, FeaturePatternEvaluator>();
            services.AddSingleton<EvaluatedValueSignNoramalizer, EvaluatedValueSignNoramalizer>();
            services.AddSingleton<MobilityAnalyzer, MobilityAnalyzer>();
            services.AddSingleton<DiscCountEvaluator, DiscCountEvaluator>();
            services.AddSingleton<FindBestMover, FindBestMover>();
            services.AddSingleton<MobilityCacher, MobilityCacher>();
            services.AddSingleton<EvalCacher, EvalCacher>();
            services.AddSingleton<ReverseResultCacher, ReverseResultCacher>();

            // Transposition Table（Task 2 で追加）
            services.AddSingleton<TranspositionTableConfig, TranspositionTableConfig>();
            services.AddSingleton<ITranspositionTable, ZobristTranspositionTable>();
            services.AddSingleton<IZobristHash, ZobristHash>();

            services.AddTransient<NegaMax, NegaMax>();
            services.AddTransient<CachedNegaMax, CachedNegaMax>();
            services.AddTransient<ISearchEngine, LegacySearchEngine>();
            services.AddTransient<PvsSearchEngine, PvsSearchEngine>();
            var provider = services.BuildServiceProvider();
            return provider;
        }
    }
}
