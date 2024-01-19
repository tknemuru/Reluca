using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reluca.Contexts
{
    /// <summary>
    /// 盤の状態を管理します。
    /// </summary>
    public record BoardContext
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
