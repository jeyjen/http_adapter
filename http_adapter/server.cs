using jeyjen.net.server;
using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using jeyjen.extension;
using Apache.NMS;
using Apache.NMS.Util;
using System.IO;

namespace http_adapter
{
    class server : web_server
    {
        private string _url;

        private Dictionary<string, Tuple<context, IMessageConsumer>> _request;
        IConnectionFactory _factory;
        IConnection _connection;
        ISession _session;

        HashSet<string> _prefixes;
        HashSet<string> _performers;
        
        public HashSet<string> prefixes
        {
            get
            {
                return _prefixes;
            }
        }

        public HashSet<string> performers
        {
            get
            {
                return _performers;
            }
        }

        public server(string activemq_url, int port, X509Certificate2 certificate = null) : base(port, certificate)
        {
            _url = activemq_url;
            _prefixes = new HashSet<string>();
            _performers = new HashSet<string>();
        }
        protected override void on_start()
        {
            _request = new Dictionary<string, Tuple<context, IMessageConsumer>>();

            _factory = new NMSConnectionFactory(_url);
            _connection = _factory.CreateConnection();
            _connection.Start();
            _session = _connection.CreateSession(AcknowledgementMode.AutoAcknowledge);
        }
        protected override void on_stop()
        {
            try
            {
                foreach (var r in _request)
                {
                    r.Value.Item1.close();
                    r.Value.Item2.Close();
                }
            }
            catch
            { }
            try
            {
                _session.Close();
            }
            catch
            { }
            try
            {
                _connection.Close();
            }
            catch
            { }
        }
        protected override void on_http_request(context context)
        {
            // не обрабатывать запрос на полученеи favicon
            var ext = Path.GetExtension(context.request.url).ToLower();
            if (!ext.is_null_or_empty() || context.request.url.is_equals("/"))
            {
                // вернуть документацию
                var fn = context.request.url.is_equals("/") ? "index.html" : context.request.url.Substring(1);
                fn = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", fn);

                var fi = new FileInfo(fn);
                if (File.Exists(fn))
                {
                    var bytes = File.ReadAllBytes(fn);
                    switch (ext)
                    {
                        case ".html":
                            {
                                context.responce.content_type = "text/html";
                            }
                            break;
                        case ".js":
                            {
                                context.responce.content_type = "text/javascript";
                            }
                            break;
                        case ".css":
                            {
                                context.responce.content_type = "text/css";
                            }
                            break;
                        case ".gif":
                            {
                                context.responce.content_type = "image/gif";
                            } break;
                        case ".png":
                            {
                                context.responce.content_type = "image/png";
                            }
                            break;
                            
                    }
                    context.responce.send(bytes);
                }
                else
                {
                    context.responce.status = status.Bad_Request;
                    context.responce.send();
                }
            }
            else
            {
                var req = context.request;
                var parts = req.url.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                string prefix = "";
                string performer = "";
                string ctx = "";
                string action = "";
                if (parts.Length > 0)
                {
                    int offset = 0;
                    if (prefixes.Contains(parts[0].ToLower()))
                    {
                        offset = 1;
                        prefix = parts[0].ToLower();
                    }
                    if (parts.Length - offset > 0)
                    {
                        performer = parts[offset].ToLower();
                    }
                    if (parts.Length - offset > 1)
                    {
                        ctx = parts[offset + 1].ToLower();
                    }
                    if (parts.Length - offset > 2)
                    {
                        action = parts[offset + 2].ToLower();
                    }
                    if (performers.Contains(performer) || performers.Contains(performer + ".info"))
                    {
                        var qn = new StringBuilder();
                        if (!prefix.is_null_or_empty())
                        {
                            qn.AppendFormat("{0}.", prefix);
                        }
                        qn.AppendFormat("@{0}", performer);
                        var prod = _session.CreateProducer(SessionUtil.GetDestination(_session, qn.ToString()));
                        var m = _session.CreateBytesMessage();
                        m.Properties["context"] = ctx;
                        m.Properties["action"] = action;

                        if (context.request.headers.ContainsKey("Content-Type"))
                        {
                            m.Properties["content-type"] = context.request.headers["Content-Type"];
                        }
                        byte[] data = null;
                        if (context.request.method.is_equals("GET"))
                        {
                            if (context.request.url_param.Count > 0)
                            {
                                var c = new flex();
                                foreach (var p in context.request.url_param)
                                {
                                    c.set(p.Key, p.Value);
                                }
                                data = Encoding.UTF8.GetBytes(c.json());
                            }
                        }
                        else
                        {
                            data = context.request.body;
                        }
                        m.Content = data;
                        var rq = SessionUtil.GetDestination(_session, null, DestinationType.TemporaryQueue);
                        var consumer = _session.CreateConsumer(rq);
                        consumer.Listener += on_responce;

                        var id = guid.generate(guid_type.at_end).ToString();
                        m.NMSCorrelationID = id;
                        _request.Add(id, new Tuple<context, IMessageConsumer>(context, consumer));
                        prod.Send(m);
                    }
                    else
                    {
                        context.responce.status = status.Bad_Request;
                        context.responce.send("обработчик \"{0}\" не определен".format(performer));
                    }
                }
                else
                {
                    context.responce.status = status.Bad_Request;
                    context.responce.send("некорректный url");
                }
            }
        }
        private void on_responce(IMessage message)
        {
            Tuple<context, IMessageConsumer> value;
            if (!message.NMSCorrelationID.is_null() &&_request.TryGetValue(message.NMSCorrelationID, out value))
            {
                byte[] data = null;
                if(message is ITextMessage)
                {
                    var text = ((ITextMessage)(message)).Text;
                    if (! text.is_null_or_empty())
                    {
                        data = Encoding.UTF8.GetBytes(text);
                    }
                }
                else
                {
                    data = ((IBytesMessage)(message)).Content;
                }

                if (message.Properties.Contains("result"))
                {
                    if (message.Properties["result"].is_equals("error"))
                    {
                        value.Item1.responce.status = status.Internal_Server_Error;
                    }
                }
                if (message.Properties.Contains("content-type"))
                {
                    value.Item1.responce.content_type = message.Properties.GetString("content-type");
                }
                value.Item1.responce.send(data);
                value.Item2.Close(); // MessageConsumer
            }
        }
        
    }
}
