using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace http_adapter
{
    class Program
    {
        static void Main(string[] args)
        {
            int port = -1;
            if (! int.TryParse(ConfigurationManager.AppSettings["port"], out port))
            {
                throw new Exception("некорректно настроен порт в файле конфигурации");
            }
            var s = new server("activemq:tcp://localhost:61616", port);

            var prefixes = ConfigurationManager.AppSettings["prefixes"].Split(";".ToCharArray());
            foreach (var p in prefixes)
            {
                s.prefixes.Add(p.Trim());
            }

            var performers = ConfigurationManager.AppSettings["performers"].Split(";".ToCharArray());
            foreach (var p in performers)
            {
                s.performers.Add(p.Trim());
            }

            s.start();
            Console.WriteLine("service start on {0} port", port);
            Console.ReadLine();
            s.stop();
        }
    }
}
