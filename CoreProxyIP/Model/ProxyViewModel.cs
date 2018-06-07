using System;
using System.Collections.Generic;
using System.Text;

namespace CoreProxyIP
{
    public class ProxyViewModel
    {
        /// <summary>
        /// IP:PORT
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// IP地址
        /// </summary>
        public string ProxyIP { get; set; }
        /// <summary>
        /// IP端口
        /// </summary>
        public int ProxyPort { get; set; }
        /// <summary>
        /// 添加时间
        /// </summary>
        public DateTime CreateTime { get; set; }
        /// <summary>
        /// 状态
        /// </summary>
        public int State { get; set; }
    }
}
