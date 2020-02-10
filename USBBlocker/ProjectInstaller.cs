using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Diagnostics;

namespace USBBlocker
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        Timer timer = new Timer();
        public ProjectInstaller()
        {
            InitializeComponent();
        }
    }
}
