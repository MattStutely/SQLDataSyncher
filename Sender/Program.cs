using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Security;

namespace SQLDataSyncSender
{
    internal class Program
    {
        private static void Main(string[] args)
        {

            var sender = new SenderService();
            sender.ProcessQueue();
        }


    }
}
