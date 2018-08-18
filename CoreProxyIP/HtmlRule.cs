using System;
using System.Collections.Generic;
using System.Text;
using HtmlAgilityPack;
using System.Threading;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;

namespace CoreProxyIP
{
    public class HtmlRule
    {
        private static string url = "http://127.0.0.1:12306/";
        /// <summary>
        /// 存放代理IP集合
        /// </summary>
        private static Queue<ProxyViewModel> _ProxyQueue = new Queue<ProxyViewModel>();
        /// <summary>
        /// 锁
        /// </summary>
        private static object _lock = new object();
        /// <summary>
        /// 队列操作
        /// </summary>
        /// <returns></returns>
        public static Dictionary<int, ProxyViewModel> QueueOperation(ProxyViewModel proxy, IQueueType type)
        {
            var result = new Dictionary<int, ProxyViewModel>();
            lock (_lock)
            {
                try
                {
                    switch (type)
                    {
                        // 归队
                        case IQueueType.EnQueue:
                            _ProxyQueue.Enqueue(proxy);
                            result.Add(1, proxy);
                            break;
                        // 出队
                        case IQueueType.DeQueue:
                            if (_ProxyQueue.Any())
                            {
                                result.Add(1, _ProxyQueue.Dequeue());
                            }
                            else
                            {
                                result.Add(0, null);
                            }
                            break;
                        // 统计个数
                        case IQueueType.CountQueue:
                            result.Add(_ProxyQueue.Count(), null);
                            break;
                        // 检测队列是否包含该元素
                        case IQueueType.Exsist:
                            var exsistList = _ProxyQueue.Any() ? _ProxyQueue.Where(p => p.Id == proxy.Id).ToList() : new List<ProxyViewModel>();
                            result.Add(exsistList.Count(), exsistList.Any() ? proxy : null);
                            break;
                        // 删除队列指定元素
                        case IQueueType.Del:
                            var total = _ProxyQueue.Count();
                            for (int i = 0; i < total; i++)
                            {
                                if (_ProxyQueue.Any())
                                {
                                    var delProxy = _ProxyQueue.Dequeue();
                                    if (delProxy.Id != proxy.Id) _ProxyQueue.Enqueue(delProxy);
                                    else
                                    {
                                        result.Add(1, proxy);
                                    }
                                }
                            }
                            break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("{0}> 队列操作异常：{1}，{2}", DateTime.Now.ToString("s"), type.ToString(), e.Message);
                }
            }
            return result;
        }


        /// <summary>
        /// 获取代理IP接口
        /// </summary>
        public static void Lisener()
        {
            Console.Title = url;

            HttpListener listerner = new HttpListener();
            {
                listerner.AuthenticationSchemes = AuthenticationSchemes.Anonymous;//指定身份验证 Anonymous匿名访问
                listerner.Prefixes.Add(url);
                listerner.Start();

                new Thread(new ThreadStart(delegate
                {
                    while (true)
                    {
                        HttpListenerContext httpListenerContext = listerner.GetContext();
                        new Thread(new ThreadStart(delegate
                        {
                            HttpListenerContext ctx = httpListenerContext;

                            try
                            {
                                using (StreamWriter writer = new StreamWriter(ctx.Response.OutputStream))
                                {
                                    ctx.Response.StatusCode = 200;

                                    string ipp = ctx.Request.QueryString["ipp"];
                                    if (null != ipp && Regex.IsMatch(ipp, @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\:\d{1,5}$"))
                                    {
                                        Console.WriteLine("{0}> 删除代理{1}", DateTime.Now.ToString("s"), ipp);

                                        QueueOperation(new ProxyViewModel() { Id = ipp }, IQueueType.Del);

                                        writer.WriteLine("true");
                                    }
                                    else
                                    {
                                        int count = 0;
                                        while (true)
                                        {
                                            if (count > 10) { writer.WriteLine("false"); break; }
                                            // 出队已个代理IP对象
                                            var que = QueueOperation(null, IQueueType.DeQueue);
                                            if (que.First().Key > 0)
                                            {
                                                // 判断该代理IP时间在5分钟内产生的直接返回使用
                                                if ((que.First().Value.CreateTime.AddMinutes(5)) > DateTime.Now)
                                                {
                                                    Console.WriteLine("{0}> 直接输出{1}", DateTime.Now.ToString("s"), que.First().Value.Id);
                                                    // 输出http响应代码
                                                    writer.WriteLine(que.First().Value.Id);
                                                    QueueOperation(que.First().Value, IQueueType.EnQueue);
                                                    break;
                                                }
                                                else
                                                {
                                                    // 验证代理IP有效性
                                                    if (DbVerIp(que.First().Value))
                                                    {
                                                        Console.WriteLine("{0}> 验证输出{1}", DateTime.Now.ToString("s"), que.First().Value.Id);
                                                        // 输出http响应代码
                                                        writer.WriteLine(que.First().Value.Id);
                                                        // 退出本次请求
                                                        break;
                                                    }
                                                }

                                            }
                                            count++;
                                            // 队列无可用代理IP情况下等待2秒再获取
                                            Thread.Sleep(TimeSpan.FromSeconds(2));
                                        }
                                    }
                                    //writer.Close();
                                    //ctx.Response.Close();
                                }
                            }
                            catch (Exception ex)
                            {
                                try
                                {
                                    Console.WriteLine("{0}> 接口異常：{1}", DateTime.Now.ToString("s"), ex.Message);

                                    using (StreamWriter writer = new StreamWriter(ctx.Response.OutputStream))
                                    {
                                        ctx.Response.StatusCode = 200;
                                        writer.WriteLine("false");
                                    }
                                }
                                catch (Exception e)
                                {
                                }
                            }

                        })).Start();
                    }
                })).Start();
            }
        }


        #region 验证代理IP

        /// <summary>
        /// 验证将要从接口出去的代理IP
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static bool DbVerIp(ProxyViewModel proxy)
        {
            bool success = false;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            try
            {
                HttpWebRequest Req;
                HttpWebResponse Resp;
                WebProxy proxyObject = new WebProxy(proxy.ProxyIP, proxy.ProxyPort);// port为端口号 整数型
                Req = WebRequest.Create("https://www.baidu.com") as HttpWebRequest;
                Req.Proxy = proxyObject; //设置代理
                Req.Timeout = 3000;   //超时
                Resp = (HttpWebResponse)Req.GetResponse();
                Encoding bin = Encoding.GetEncoding("UTF-8");
                using (StreamReader sr = new StreamReader(Resp.GetResponseStream(), bin))
                {
                    string str = sr.ReadToEnd();
                    if (str.Contains("百度"))
                    {
                        Resp.Close();
                        // 更新验证时间
                        proxy.CreateTime = DateTime.Now;
                        // 更新验证状态
                        proxy.State = 1;
                        // 记录验证状态
                        success = true;
                        // 验证通过，归队
                        QueueOperation(proxy, IQueueType.EnQueue);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("{0}> 接口验证异常{1}", DateTime.Now.ToString("s"), e.Message);
            }
            sw.Stop();
            Console.WriteLine("{0}> 接口验证{1} {2} 耗时{3}s", DateTime.Now.ToString("s"), proxy.Id, success, sw.ElapsedMilliseconds / 1000.00);
            return success;
        }


        /// <summary>
        /// 验证list集合里面的代理IP
        /// </summary>
        /// <param name="msg"></param>
        public static void ProxyVerification(object msg, string name)
        {
            if (null == msg) return;
            ProxyViewModel proxy = (ProxyViewModel)msg;
            try
            {
                using (WebClient web = new WebClient())
                {
                    try
                    {
                        HttpWebRequest Req;
                        HttpWebResponse Resp;
                        WebProxy proxyObject = new WebProxy(proxy.ProxyIP, proxy.ProxyPort);
                        Req = WebRequest.Create("https://www.baidu.com") as HttpWebRequest;
                        Req.Proxy = proxyObject; //设置代理
                        Req.Timeout = 3000;   //超时
                        Resp = (HttpWebResponse)Req.GetResponse();
                        Encoding bin = Encoding.GetEncoding("UTF-8");
                        using (StreamReader sr = new StreamReader(Resp.GetResponseStream(), bin))
                        {
                            string str = sr.ReadToEnd();
                            if (str.Contains("百度"))
                            {
                                Resp.Close();
                                // 更新验证时间
                                proxy.CreateTime = DateTime.Now;
                                // 更新验证状态
                                proxy.State = 1;
                                // 验证通过，归队
                                QueueOperation(proxy, IQueueType.EnQueue);
                                Console.WriteLine("{0}> [{2}]自动验证成功{1}", DateTime.Now.ToString("s"), proxy.Id, name);
                            }
                            else { Console.WriteLine("{0}> [{2}]自动验证失败{1}", DateTime.Now.ToString("s"), proxy.Id, name); }
                        }
                    }
                    catch (Exception ex)
                    { }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("{0}> [{3}]自动验证异常{1} {2}", DateTime.Now.ToString("s"), proxy.Id, e.Message, name);
            }
        }

        #endregion


        #region 获取代理IP

        /// <summary>
        /// 流年
        /// </summary>
        public static void Liunian()
        {
            while (true)
            {
                try
                {
                    string LiunianUrl = "http://www.89ip.cn/";
                    var html = IWebClient(LiunianUrl);
                    if (!string.IsNullOrEmpty(html))
                    {
                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(html);
                        var table = doc.DocumentNode.SelectSingleNode("//table[@class='layui-table']");
                        var trlist = table.SelectNodes("//tr").ToList();
                        for (int i = 1; i < trlist.Count; i++)
                        {
                            try
                            {
                                var iplist = trlist[i].SelectNodes("td").ToList();
                                var ip = iplist[0].InnerText.Trim();
                                var port = iplist[1].InnerText.Trim();
                                ProxyViewModel proxy = new ProxyViewModel() { Id = string.Format("{0}:{1}", ip, port), ProxyIP = ip, ProxyPort = Convert.ToInt32(port), CreateTime = DateTime.Now, State = 0 };
                                //判断Ip是否已经存在
                                if (QueueOperation(proxy, IQueueType.Exsist).First().Key > 0) continue;
                                #region 启用多线程去验证
                                IList<Task> itasks = new List<Task>();
                                CancellationTokenSource isoure = new CancellationTokenSource();
                                CancellationToken itoken = isoure.Token;
                                itasks.Add(new Task(() =>
                                {
                                    try
                                    {
                                        ProxyViewModel taskProxy = new ProxyViewModel()
                                        {
                                            Id = proxy.Id,
                                            CreateTime = proxy.CreateTime,
                                            ProxyIP = proxy.ProxyIP,
                                            ProxyPort = proxy.ProxyPort,
                                            State = proxy.State
                                        };
                                        ProxyVerification(taskProxy, "流年");
                                    }
                                    catch (Exception ex)
                                    { }
                                }, itoken));
                                itasks[0].Start();
                                Task.WaitAll(itasks.ToArray(), (4 * 1000), itoken);
                                #endregion
                            }
                            catch (Exception ex)
                            { }
                        }
                    }
                }
                catch (Exception e)
                { }
            }
        }
        /// <summary>
        /// 获取西刺首页代理
        /// </summary>
        public static void Xici()
        {
            while (true)
            {
                try
                {
                    string url = "http://www.xicidaili.com/";
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(IWebClient(url));
                    var table = doc.DocumentNode.SelectSingleNode("//table[@id='ip_list']");
                    var tdList = table.SelectNodes("//tr").ToList();
                    for (int i = 0; i < tdList.Count; i++)
                    {
                        try
                        {
                            var td = tdList[i].SelectNodes("td").ToList();
                            if (td.Count != 8) continue;
                            var ip = td[1].InnerText;
                            int port = Convert.ToInt32(td[2].InnerText);
                            ProxyViewModel proxy = new ProxyViewModel() { Id = string.Format("{0}:{1}", ip, port), ProxyIP = ip, ProxyPort = port, CreateTime = DateTime.Now, State = 0 };
                            if (QueueOperation(proxy, IQueueType.Exsist).First().Key > 0) continue;
                            ProxyVerification(proxy, "西刺");
                        }
                        catch (Exception e)
                        { }
                    }
                }
                catch (Exception e)
                { }
                Thread.Sleep(TimeSpan.FromMinutes(1));
            }
        }
        /// <summary>
        /// 获取西刺高匿代理
        /// </summary>
        /// <returns></returns>
        public static string XiciGaoni()
        {
            while (true)
            {
                try
                {
                    string url = "http://www.xicidaili.com/nn/";
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(IWebClient(url));
                    var tdList = doc.DocumentNode.SelectNodes("//tr[@class='odd']").ToList();
                    for (int i = 0; i < tdList.Count; i++)
                    {
                        try
                        {
                            var td = tdList[i].SelectNodes("td").ToList();
                            var ip = td[1].InnerText;
                            int port = Convert.ToInt32(td[2].InnerText);
                            ProxyViewModel proxy = new ProxyViewModel() { Id = string.Format("{0}:{1}", ip, port), ProxyIP = ip, ProxyPort = port, CreateTime = DateTime.Now, State = 0 };
                            if (QueueOperation(proxy, IQueueType.Exsist).First().Key > 0) continue;
                            ProxyVerification(proxy, "西刺nn");
                        }
                        catch (Exception e)
                        { }
                    }
                }
                catch (Exception e)
                { }
                Thread.Sleep(TimeSpan.FromMinutes(1));
            }
        }
        /// <summary>
        /// 获取西刺高匿代理
        /// </summary>
        /// <returns></returns>
        public static void XiciPutong()
        {
            while (true)
            {
                try
                {
                    string url = "http://www.xicidaili.com/nt/";
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(IWebClient(url));
                    var tdList = doc.DocumentNode.SelectNodes("//tr[@class='odd']").ToList();
                    for (int i = 0; i < tdList.Count; i++)
                    {
                        try
                        {
                            var td = tdList[i].SelectNodes("td").ToList();
                            var ip = td[1].InnerText;
                            int port = Convert.ToInt32(td[2].InnerText);
                            ProxyViewModel proxy = new ProxyViewModel() { Id = string.Format("{0}:{1}", ip, port), ProxyIP = ip, ProxyPort = port, CreateTime = DateTime.Now, State = 0 };
                            if (QueueOperation(proxy, IQueueType.Exsist).First().Key > 0) continue;
                            ProxyVerification(proxy, "西刺nt");
                        }
                        catch (Exception e)
                        { }
                    }
                }
                catch (Exception e)
                { }
                Thread.Sleep(TimeSpan.FromMinutes(1));
            }
        }
        /// <summary>
        /// 获取页面的HTML
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string IWebClient(string url)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Headers.Add("Cookie", "__cfduid=dbc0747d4f20e880b1f3fbeddd7ee7f9b1518096123; Hm_lvt_24b7d5cc1b26f24f256b6869b069278e=1518096226; yjs_id=4b3a274b8bb0be2202978cee06964563; yd_cookie=5bc973fb-f37b-425614221fbda95de6a441f2298ff2543cf1; UM_distinctid=1654c7c2cf02a0-09d94a3256c3ad-514d2f1f-144000-1654c7c2cf35bb; CNZZDATA1254651946=1879619778-1534585157-%7C1534585157; Hm_lvt_8ccd0ef22095c2eebfe4cd6187dea829=1534586531; Hm_lpvt_8ccd0ef22095c2eebfe4cd6187dea829=1534586545");
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/68.0.3440.75 Safari/537.36";
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                StreamReader sr = new StreamReader(response.GetResponseStream());
                return sr.ReadToEnd();
            }
            catch (Exception e)
            {
                return string.Empty;
            }
        }

        #endregion
    }
}
