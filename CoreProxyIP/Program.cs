using System;
using System.Threading.Tasks;

namespace CoreProxyIP
{
    class Program
    {
        static void Main(string[] args)
        {
            //代理IP监听接口
            Task.Run(() => HtmlRule.Lisener());

            //获取代理IP
            Task.Run(() => HtmlRule.Liunian());
            Task.Run(() => HtmlRule.Xici());
            Task.Run(() => HtmlRule.XiciGaoni());
            Task.Run(() => HtmlRule.XiciPutong());


            Console.ReadLine();
        }
    }
}
