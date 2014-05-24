using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceLibrary
{
    public class MonitorService : IMonitorService
    {

        private SystemStats GetHardwareStats()
        {
            var monitor = new SystemMonitor();
            var driveInfos = new List<DriveInfo>();
            foreach (string drive in ConfigurationManager.AppSettings["monitordrives"].Split(Convert.ToChar(",")))
            {
             driveInfos.Add(monitor.DriveInfo(drive + @":\"));   
            }
            return new SystemStats
            {
                CPU = monitor.CPUUsage(),
                DiskUsage = monitor.DiskUsage(),
                MemoryInfo = monitor.MemoryInfo(),
                Drives = driveInfos,
                ComputerName=monitor.ComputerName(),
                Description=monitor.Description(),
                IPAddress = monitor.IPAddress()
            };
        }

        public SystemStats GetHardwareStatsJson()
        {
            return GetHardwareStats();
        }

        public SystemStats GetHardwareStatsXml()
        {
            return GetHardwareStats();
        }
    }
}
