using Bend.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace ConsoleApplication1
{
    partial class BirkaPrintService : ServiceBase
    {
        public BirkaPrintService()
        {
            InitializeComponent();
            new ServiceStarter();
        }

        protected override void OnStart(string[] args)
        {
            
        }

        protected override void OnStop()
        {
        }

        public static void Main()
        {
            System.ServiceProcess.ServiceBase[] ServicesToRun;
            //Change the following line to match. 
            ServicesToRun = new System.ServiceProcess.ServiceBase[] { new BirkaPrintService() };
            System.ServiceProcess.ServiceBase.Run(ServicesToRun);
        }
    }
}
