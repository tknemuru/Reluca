/// <summary>
/// 【ModuleDoc】
/// 責務: アプリケーション全体の依存性注入コンテナを管理する
/// 入出力: なし（静的プロバイダ）
/// 副作用: 初回アクセス時にサービスコンテナを構築
/// </summary>
using Microsoft.Extensions.DependencyInjection;
using Reluca.Analyzers;
using Reluca.Cachers;
using Reluca.Contexts;
using Reluca.Converters;
using Reluca.Evaluates;
using Reluca.Movers;
using Reluca.Search;
using Reluca.Serchers;
using Reluca.Services;
using Reluca.Updaters;
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
            var services = new ServiceCollection();
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

            services.AddTransient<NegaMax, NegaMax>();
            services.AddTransient<CachedNegaMax, CachedNegaMax>();
            services.AddTransient<ISearchEngine, LegacySearchEngine>();
            var provider = services.BuildServiceProvider();
            return provider;
        }
    }
}
