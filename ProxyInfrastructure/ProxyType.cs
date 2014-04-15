namespace SocksProxy
{
    /// <summary>
    /// Specifies the type of proxy servers that an instance of the ProxySocket class can use.
    /// </summary>
    public enum ProxyType
    {
        /// <summary>No proxy server; the ProxySocket object behaves exactly like an ordinary Socket object.</summary>
        None,
        /// <summary>A SOCKS4[A] proxy server.</summary>
        Socks4,
        /// <summary>A SOCKS5 proxy server.</summary>
        Socks5
    }
}