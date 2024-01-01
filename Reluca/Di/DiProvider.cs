using Microsoft.Extensions.DependencyInjection;
using Reluca.Converters;
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
            services.AddTransient<BoardContext, BoardContext>();
            var provider = services.BuildServiceProvider();
            return provider;
        }
    }
}
