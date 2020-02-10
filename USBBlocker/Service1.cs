using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Management;
using System.Runtime.InteropServices;
using System.IO;


namespace USBBlocker
{
    public partial class Service1 : ServiceBase
    {
        Timer timer = new Timer();
        ManagementEventWatcher insertWatcher = new ManagementEventWatcher();
        EventLog eventos = new EventLog();
        Boolean TrainMode = false;
        string path = @"C:\ProgramData\USBSignatures.txt";

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            //timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
            //timer.Interval = 1000; //number in milisecinds  
            //timer.Enabled = true;
            //Check if new USB has beed added

            //Event type=3 is removed
            WqlEventQuery insertQuery = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2");
            insertWatcher.Query = insertQuery;
            insertWatcher.EventArrived += new EventArrivedEventHandler(Monitorize);
            insertWatcher.Start();

            //Creates the eventslog
            eventos.Log = "Aplicación";
            ((ISupportInitialize)(this.EventLog)).BeginInit();
            if (!EventLog.SourceExists(this.EventLog.Source))
            {
                EventLog.CreateEventSource(this.EventLog.Source, this.EventLog.Log);
            }
            ((ISupportInitialize)(this.EventLog)).EndInit();

            this.EventLog.WriteEntry("El Servicio USBBlock ha comenzado", EventLogEntryType.Information);
        }

        protected override void OnStop()
        {
            this.EventLog.WriteEntry("El Servicio USBBlock ha Finalizado", EventLogEntryType.Information);
        }

        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern bool WTSDisconnectSession(IntPtr hServer, int sessionId, bool bWait);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern int WTSEnumerateSessions(IntPtr hServer, int Reserved, int Version, ref IntPtr ppSessionInfo, ref int pCount);

        [DllImport("wtsapi32.dll")]
        static extern void WTSFreeMemory(IntPtr pMemory);

        [StructLayout(LayoutKind.Sequential)]
        private struct WTS_SESSION_INFO
        {
            public Int32 SessionID;

            [MarshalAs(UnmanagedType.LPStr)]
            public String pWinStationName;

            public WTS_CONNECTSTATE_CLASS State;
        }

        private enum WTS_INFO_CLASS
        {
            WTSInitialProgram,
            WTSApplicationName,
            WTSWorkingDirectory,
            WTSOEMId,
            WTSSessionId,
            WTSUserName,
            WTSWinStationName,
            WTSDomainName,
            WTSConnectState,
            WTSClientBuildNumber,
            WTSClientName,
            WTSClientDirectory,
            WTSClientProductId,
            WTSClientHardwareId,
            WTSClientAddress,
            WTSClientDisplay,
            WTSClientProtocolType
        }

        private enum WTS_CONNECTSTATE_CLASS
        {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        }

        private void Monitorize(object sender, EventArrivedEventArgs e)
        {
            // Get time when new USB is plugged in
            ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent;
            this.EventLog.WriteEntry(String.Concat("[USBBlocker] Se ha detectado un nuevo USB: TIME_CREATED= ", instance.GetPropertyValue("TIME_CREATED")), EventLogEntryType.Information);
            String props = "";
            foreach (var property in instance.Properties)
            {
                props = String.Concat(props, property.Name, " = ", property.Value, " ;");
            }
            //this.EventLog.WriteEntry(String.Concat("[USBBlocker] Se ha detectado un nuevo USB: ", props),EventLogEntryType.Information);

            List<string> devices_plugged = new List<string>();
            try
            {
                devices_plugged = devices_plugged.Union(list_properties("SELECT * FROM Win32_Keyboard", "Win32_Keyboard")).ToList();
                devices_plugged = devices_plugged.Union(list_properties("SELECT * FROM CIM_USBDevice", "CIM_USBDevice")).ToList();
                devices_plugged = devices_plugged.Union(list_properties("SELECT * FROM Win32_USBHub", "Win32_USBHub")).ToList();
                devices_plugged = devices_plugged.Union(list_properties("SELECT * FROM Win32_MemoryDevice", "Win32_MemoryDevice")).ToList();
                //devices_plugged = devices_plugged.AddRange(list_properties("SELECT * FROM Win32_USBControllerDevice", "Win32_USBControllerDevice"));
            }
            catch (Exception) { }

            Check_devices(devices_plugged);
        }

        private List<string> list_properties(String query, String device)
        {
            ManagementObjectSearcher mgmtObjSearcher = new ManagementObjectSearcher(query);
            ManagementObjectCollection objCol = mgmtObjSearcher.Get();
            String devices_ID_string = "";
            var devices_ID = new List<string>();
            foreach (ManagementObject obj in objCol)
            {
                String deviceID = (string)obj["DeviceID"];
                devices_ID.Add(deviceID);
                devices_ID_string = String.Concat(devices_ID_string, deviceID, ";;");
            }
            this.EventLog.WriteEntry(String.Concat("[USBBlocker] Informacion del dispositivo ", device, " : ", "DEVICES ID=", devices_ID_string), EventLogEntryType.Information);
            return devices_ID;
        }

        private void Check_devices(List<string> devices_ID)
        {
            string[] Recognised_devices = Accepted_Devices();
            foreach (string devID in devices_ID)
            {
                // BashBunny found
                if (devID.Contains("F000"))
                {
                    this.EventLog.WriteEntry(String.Concat("[USBBlocker] PC Bloqueado, causa: BashBunny. Found device ID ", devID), EventLogEntryType.Warning);
                    BlockComputer();
                }

                //Now is a Whitelist
                else if (!Recognised_devices.Contains(devID))
                {
                    this.EventLog.WriteEntry(String.Concat("[USBBlocker] Se ha detectado un dispositivo no reconocido ", devID), EventLogEntryType.Information);
                    if (TrainMode)
                    {
                        this.EventLog.WriteEntry(String.Concat("[USBBlocker] Se ha añadido un nuevo dispositivo a las firmas reconocidas", devID), EventLogEntryType.Warning);
                        // true as secon arg enables concat instead of overwrite.
                        using (StreamWriter sw = new StreamWriter(path,true))
                        {
                            sw.WriteLine(devID);
                        }
                    }
                    else
                    { 
                        this.EventLog.WriteEntry(String.Concat("[USBBlocker]  Bloqueando, se ha introducido un nuevo dispositivo ", devID), EventLogEntryType.Warning);
                        BlockComputer();
                    }
                }
            }
        }

        //Devices can be added manually under "path" var file
        private string[] Accepted_Devices()
        {
            //default recognized ones. "@" before string is for non escape the backslash
            string[] Recognised_devices = { @"USB\VID_10D5&PID_000D&MI_00\7&2F53004F&0&0000", @"ACPI\LEN0071\4&39D7568D&0", @"USB\VID_17EF&PID_608C&MI_00\7&8AE0656&0&0000", @"USB\ROOT_HUB30\4&318E91B5&1&0", @"USB\VID_04CA&PID_7058\5&2AFD7BB9&0&8", @"USB\VID_2109&PID_2811\5&2AFD7BB9&0&4", @"USB\VID_10D5&PID_000D\6&82E9074&0&3", @"USB\VID_05E3&PID_0608\5&2AFD7BB9&0&3", @"USB\VID_2109&PID_8110\5&2AFD7BB9&0&16", @"USB\VID_17EF&PID_608C\6&82E9074&0&1" };
            List<string> Recognised_devices_list = new List<string>(Recognised_devices);

            List<string> lines_list = new List<string>();

            if (!File.Exists(path))
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(path))
                    {
                        sw.WriteLine("Train_mode=True");
                    }
                    this.EventLog.WriteEntry(String.Concat("Se ha generado el fichero ", path), EventLogEntryType.Information);
                }
                catch (Exception e)
                {
                    this.EventLog.WriteEntry(String.Concat("No se ha podido crear el fichero", path, " por ",e.ToString()), EventLogEntryType.Information);
                }
            }
            else
            {
                try
                {
                    string[] lines = File.ReadAllLines(path);
                    lines_list = new List<string>(lines);
                    this.EventLog.WriteEntry(String.Concat("[USBBlocker] Se han leido algunas firmas del fichero: ", string.Join(";;", lines_list)), EventLogEntryType.Information);

                    //check if train mode
                    if (lines_list.Contains("Train_mode=True"))
                    {
                        TrainMode = true;
                    }
                    else
                    {
                        TrainMode = false;
                    }
                }
                catch (ArgumentNullException) { }
            }

            try { Recognised_devices_list = Recognised_devices_list.Union(lines_list).ToList(); } catch (ArgumentNullException) { }

            return Recognised_devices_list.ToArray();
        }

        private void BlockComputer()
        {

            IntPtr ppSessionInfo = IntPtr.Zero;
            Int32 count = 0;
            Int32 retval = WTSEnumerateSessions(IntPtr.Zero, 0, 1, ref ppSessionInfo, ref count);
            Int32 dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
            Int32 currentSession = (int)ppSessionInfo;

            if (retval == 0) return;

            for (int i = 0; i < count; i++)
            {
                WTS_SESSION_INFO si = (WTS_SESSION_INFO)Marshal.PtrToStructure((System.IntPtr)currentSession, typeof(WTS_SESSION_INFO));
                if (si.State == WTS_CONNECTSTATE_CLASS.WTSActive) WTSDisconnectSession(IntPtr.Zero, si.SessionID, false);
                currentSession += dataSize;
            }
            WTSFreeMemory(ppSessionInfo);
        }
    }
}
