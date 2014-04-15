namespace SocksProxy.Web
{
    using System;
    using System.Net;

    public class WebRequestFactory : IWebRequestFactory
    {
        public WebRequest Create(Uri uri, ProxyType proxyType, string host, int port, string username, string password)
        {
            if (proxyType == ProxyType.Socks5)
            {
                var request = (SocksHttpWebRequest) SocksHttpWebRequest.Create(uri);

                request.ProxyType = proxyType;
                request.Proxy = new WebProxy(host, port) {Credentials = new NetworkCredential(username, password)};

                return request;
            }

            var webRequest = WebRequest.Create(uri);
            webRequest.Proxy = new WebProxy();
            return webRequest;
        }

        public WebRequest Create(string url, ProxyType proxyType, string host, int port, string username,
                                 string password)
        {
            if (proxyType == ProxyType.Socks5)
            {
                var request = (SocksHttpWebRequest) SocksHttpWebRequest.Create(url);

                request.ProxyType = proxyType;
                request.Proxy = new WebProxy(host, port) {Credentials = new NetworkCredential(username, password)};

                return request;
            }

            var webRequest = WebRequest.Create(url);
            webRequest.Proxy = new WebProxy();
            return webRequest;
        }
    }
}