using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace DashboardServices.ServiceAPI
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.MapHttpAttributeRoutes();
            config.EnableSystemDiagnosticsTracing();
            config.Formatters.XmlFormatter.UseXmlSerializer = true;
        }
    }
}
