namespace SocksProxy.Web
{
    using System;
    using System.Collections.Specialized;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using SocketSocks;

    public class SocksHttpWebRequest : WebRequest
    {

        #region Member Variables

        private readonly Uri _requestUri;

        private WebHeaderCollection _httpRequestHeaders;
        private string _method;
        private SocksHttpWebResponse _response;
        private string _requestMessage;
        private byte[] _requestContentBuffer;

        static readonly StringCollection ValidHttpVerbs =
            new StringCollection { "GET", "HEAD", "POST", "PUT", "DELETE", "TRACE", "OPTIONS" };

        #endregion

        #region Constructor

        private SocksHttpWebRequest(Uri requestUri)
        {
            _requestUri = requestUri;
        }

        #endregion

        #region WebRequest Members

        public override WebResponse GetResponse()
        {
            if (Proxy == null)
                throw new InvalidOperationException("Proxy property cannot be null.");

            if (Proxy is WebProxy == false)
                throw new ArgumentException("Invalid proxy");

            if (String.IsNullOrEmpty(Method))
                throw new InvalidOperationException("Method has not been set.");

            if (RequestSubmitted)
                return _response;

            try
            {
                _response = InternalGetResponse();
            }
            catch (SocketException e)
            {
                throw new ProxyException(string.Format("Socket error code {0}.", e.ErrorCode));
            }

            RequestSubmitted = true;
            return _response;
        }

        public override Uri RequestUri
        {
            get { return _requestUri; }
        }

        public override IWebProxy Proxy { get; set; }

        public override WebHeaderCollection Headers
        {
            get { return _httpRequestHeaders ?? (_httpRequestHeaders = new WebHeaderCollection()); }
            set
            {
                if (RequestSubmitted)
                    throw new InvalidOperationException("This operation cannot be performed after the request has been submitted.");

                _httpRequestHeaders = value;
            }
        }

        public bool RequestSubmitted { get; private set; }

        public override string Method
        {
            get
            {
                return _method ?? "GET";
            }
            set
            {
                if (ValidHttpVerbs.Contains(value))
                    _method = value;
                else
                    throw new ArgumentOutOfRangeException("value", string.Format("'{0}' is not a known HTTP verb.", value));
            }
        }

        public override long ContentLength { get; set; }

        public override string ContentType { get; set; }

        public override Stream GetRequestStream()
        {
            if (RequestSubmitted)
                throw new InvalidOperationException("This operation cannot be performed after the request has been submitted.");

            if (_requestContentBuffer == null)
            {
                _requestContentBuffer = new byte[ContentLength];
            }
            else if (ContentLength == default(long))
            {
                _requestContentBuffer = new byte[int.MaxValue];
            }
            else if (_requestContentBuffer.Length != ContentLength)
            {
                Array.Resize(ref _requestContentBuffer, (int)ContentLength);
            }
            return new MemoryStream(_requestContentBuffer);
        }

        #endregion

        #region Methods

        public new static WebRequest Create(string requestUri)
        {
            return new SocksHttpWebRequest(new Uri(requestUri));
        }

        public new static WebRequest Create(Uri requestUri)
        {
            return new SocksHttpWebRequest(requestUri);
        }

        public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
        {
            var asyncResult = new AsyncProxyResult();
            asyncResult.Init(state);

            var bundle = new[] { state, asyncResult, callback};
            
            ThreadPool.QueueUserWorkItem(Perform, bundle);
            
            return asyncResult;
        }

        private void Perform(object bundle)
        {
            var array = (object[]) bundle;

            var state = (object[]) (array[0]);
            var asyncResult = (AsyncProxyResult) (array[1]);
            var callback = (AsyncCallback) (array[2]);

            try
            {
                var webRequest = (SocksHttpWebRequest) state[0];
                webRequest.GetResponse();

                asyncResult.Complete();
            }
            catch(Exception e)
            {
                asyncResult.Complete(e);
            }
            finally
            {
                callback(asyncResult);
            }
        }

        public override WebResponse EndGetResponse(IAsyncResult asyncResult)
        {
            if (asyncResult.IsCompleted == false)
                asyncResult.AsyncWaitHandle.WaitOne();

            if(asyncResult == null)
                throw new ArgumentNullException("asyncResult");

            var savedException = ((AsyncProxyResult) asyncResult).SavedException;
            if(savedException != null)
                throw savedException;

            return _response;
        }

        private string BuildHttpRequestMessage()
        {
            if (RequestSubmitted)
                throw new InvalidOperationException("This operation cannot be performed after the request has been submitted.");

            var message = new StringBuilder();
            message.AppendFormat("{0} {1} HTTP/1.0\r\nHost: {2}\r\n", Method, RequestUri.PathAndQuery, RequestUri.Host);

            // add the headers
            foreach (var key in Headers.Keys)
                message.AppendFormat("{0}: {1}\r\n", key, Headers[key.ToString()]);

            if (string.IsNullOrEmpty(ContentType) == false)
                message.AppendFormat("Content-Type: {0}\r\n", ContentType);

            if (ContentLength > 0)
                message.AppendFormat("Content-Length: {0}\r\n", ContentLength);

            // add a blank line to indicate the end of the headers
            message.Append("\r\n");

            // add content
            if (_requestContentBuffer != null && _requestContentBuffer.Length > 0)
            {
                using (var stream = new MemoryStream(_requestContentBuffer, false))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        message.Append(reader.ReadToEnd());
                    }
                }
            }

            return message.ToString();
        }

        private SocksHttpWebResponse InternalGetResponse()
        {
            var response = new StringBuilder();
            using (var socksConnection = new ProxySocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                var webProxy = (WebProxy) Proxy;
                
                socksConnection.ProxyType = ProxyType;
                socksConnection.ProxyUser = ((NetworkCredential)webProxy.Credentials).UserName;
                socksConnection.ProxyPass = ((NetworkCredential)webProxy.Credentials).Password;
                socksConnection.ProxyEndPoint = new IPEndPoint(Dns.GetHostAddresses(webProxy.Address.Host)[0],
                                                               webProxy.Address.Port);

                // open connection
                socksConnection.Connect(RequestUri.Host, 80);

                // send an HTTP request
                socksConnection.Send(Encoding.ASCII.GetBytes(RequestMessage));
                
                // read the HTTP reply
                var buffer = new byte[1024];

                var bytesReceived = socksConnection.Receive(buffer);
                if (bytesReceived == 0)
                    throw new ProxyException(5);

                while (bytesReceived > 0)
                {
                    response.Append(Encoding.ASCII.GetString(buffer, 0, bytesReceived));
                    bytesReceived = socksConnection.Receive(buffer);
                }
            }
            return new SocksHttpWebResponse(response.ToString());
        }

        public override void Abort()
        {
        }

        #endregion

        #region Properties

        public string RequestMessage
        {
            get
            {
                if (string.IsNullOrEmpty(_requestMessage))
                    _requestMessage = BuildHttpRequestMessage();

                return _requestMessage;
            }
        }

        public ProxyType ProxyType { get; set; }

        #endregion

    }
}