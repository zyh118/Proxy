using System;
using System.Collections.Generic;
using System.Text;

namespace CoreProxyIP
{
    public enum IQueueType
    {
        /// <summary>
        /// 归队
        /// </summary>
        EnQueue = 1,
        /// <summary>
        /// 出队
        /// </summary>
        DeQueue = 2,
        /// <summary>
        /// 统计个数
        /// </summary>
        CountQueue = 4,
        /// <summary>
        /// 检测
        /// </summary>
        Exsist = 8,
        /// <summary>
        /// 删除指定对象
        /// </summary>
        Del = 16,
    }
}
