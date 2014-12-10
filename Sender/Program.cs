﻿using System;
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
            log4net.Config.XmlConfigurator.Configure();
            ServiceAccountProxy.SetDefault();
            var sender = new SenderService();
            sender.ProcessQueue();
        }


    }
}
