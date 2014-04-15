namespace SocksProxy.Web
{
    using System;
    using System.Net;

    public interface IWebRequestFactory
    {
        WebRequest Create(Uri uri, ProxyType proxyType, string host, int port, string username, string password);
        WebRequest Create(string url, ProxyType proxyType, string host, int port, string username, string password);
    }
}