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
        Boolean TrainMode = true;
        Boolean Block = true;
        Boolean reset;
        long epoch_old;

        //Configurable variables
        //string[] Recognised_devices_main = { @"USB\VID_10D5&PID_000D&MI_00\7&2F53004F&0&0000", @"ACPI\LEN0071\4&39D7568D&0", @"USB\VID_17EF&PID_608C&MI_00\7&8AE0656&0&0000", @"USB\ROOT_HUB30\4&318E91B5&1&0", @"USB\VID_04CA&PID_7058\5&2AFD7BB9&0&8", @"USB\VID_2109&PID_2811\5&2AFD7BB9&0&4", @"USB\VID_10D5&PID_000D\6&82E9074&0&3", @"USB\VID_05E3&PID_0608\5&2AFD7BB9&0&3", @"USB\VID_2109&PID_8110\5&2AFD7BB9&0&16", @"USB\VID_17EF&PID_608C\6&82E9074&0&1" };
        string[] Recognised_devices_main = { };
        string path = @"C:\ProgramData\USBSignatures.txt"; //default
        string logname = "Aplicación";
        int maxBlocks = 3;
        int min_secs = 2;

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            //Produces an event whenever a USB device is attached
            //Event type=2 device is attached
            //Event type=3 device is removed
            WqlEventQuery insertQuery = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2");
            insertWatcher.Query = insertQuery;
            insertWatcher.EventArrived += new EventArrivedEventHandler(Monitorize);
            insertWatcher.Start();

            //initialize variables
            epoch_old = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            //Creates the eventslog
            //Change to 'Application' if the OS language is EN. 
            if (!EventLog.SourceExists(logname))
            {
                eventos.Log = logname; 
            }
            else
            {
                eventos.Log = "Application";
            }
            ((ISupportInitialize)(this.EventLog)).BeginInit();
            if (!EventLog.SourceExists(this.EventLog.Source))
            {
                EventLog.CreateEventSource(this.EventLog.Source, this.EventLog.Log);
            }
            ((ISupportInitialize)(this.EventLog)).EndInit();

            this.EventLog.WriteEntry("[USBBlocker] service has started", EventLogEntryType.Information);
        }

        protected override void OnStop()
        {
            this.EventLog.WriteEntry("[USBBlocker] service has ended", EventLogEntryType.Information);
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
            this.EventLog.WriteEntry(String.Concat("[USBBlocker] New USB device has been detected: TIME_CREATED= ", instance.GetPropertyValue("TIME_CREATED")), EventLogEntryType.Information);
            reset = true;

            List<string> devices_plugged = new List<string>();
            try
            {
                devices_plugged = devices_plugged.Union(list_properties("SELECT * FROM Win32_Keyboard", "Win32_Keyboard")).ToList();
                devices_plugged = devices_plugged.Union(list_properties("SELECT * FROM CIM_USBDevice", "CIM_USBDevice")).ToList();
                devices_plugged = devices_plugged.Union(list_properties("SELECT * FROM Win32_USBHub", "Win32_USBHub")).ToList();
                devices_plugged = devices_plugged.Union(list_properties("SELECT * FROM Win32_MemoryDevice", "Win32_MemoryDevice")).ToList();
            } catch (Exception) { }
        
            Check_devices(devices_plugged);
            //Reset the counter Number_blocks if no new device has been plugged in
            if (reset)
            {
                Reset_num_block();
            }
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
            this.EventLog.WriteEntry(String.Concat("[USBBlocker] Device information ", device, " : ", "DEVICES ID=", devices_ID_string), EventLogEntryType.Information);
            return devices_ID;
        }

        private void Check_devices(List<string> devices_ID)
        {
            string[] Recognised_devices = Accepted_Devices();
            foreach (string devID in devices_ID)
            {
                // BashBunny found
                if (Block)
                {
                    if (devID.Contains("F000"))
                    {
                        this.EventLog.WriteEntry(String.Concat("[USBBlocker] System blocked, cause: BashBunny. Found device ID: ", devID), EventLogEntryType.Warning);
                        BlockComputer();
                    }

                    //Parses the Whitelist. Also dont block if maximun_blocks exceeded
                    else if (!Recognised_devices.Contains(devID))
                    {
                        this.EventLog.WriteEntry(String.Concat("[USBBlocker] unrecognized devide detected: ", devID), EventLogEntryType.Information);
                        if (TrainMode)
                        {
                            this.EventLog.WriteEntry(String.Concat("[USBBlocker] A new device signature has been added: ", devID), EventLogEntryType.Warning);
                            // true as secon arg enables concat instead of overwrite.
                            using (StreamWriter sw = new StreamWriter(path, true))
                            {
                                sw.WriteLine(devID);
                            }
                        }
                        else
                        {
                            this.EventLog.WriteEntry(String.Concat("[USBBlocker]  Blocking ... a new USB device has been plugged in ", devID), EventLogEntryType.Warning);
                            BlockComputer();
                        }
                    }
                }
            }
        }

        //Devices can be added manually under "path" var file
        private string[] Accepted_Devices()
        {
            //my default recognized ones. "@" before string is for non escape the backslash. You should subssitute yours instead
            List<string> Recognised_devices_list = new List<string>(Recognised_devices_main);

            List<string> lines_list = new List<string>();

            //reset if changed from last execution
            Block = true;
            if (!File.Exists(path))
            {
                Initialize_File(path);
            }
            else
            {
                try
                {
                    string[] lines = File.ReadAllLines(path);
                    lines_list = new List<string>(lines);
                    this.EventLog.WriteEntry(String.Concat("[USBBlocker] Signature file readed: ", string.Join(";;", lines_list)), EventLogEntryType.Information);

                    //check if train mode
                    if (lines_list.Contains("Train_mode=True"))
                    {
                        TrainMode = true;
                    }
                    else
                    {
                        TrainMode = false;
                    }
                    Block = check_num_block();
                }
                catch (Exception e)
                {
                    this.EventLog.WriteEntry(String.Concat("[USBBlocker] An error occurred while reading the signature file: ", e.ToString()),  EventLogEntryType.Error);
                }
            }

                try { Recognised_devices_list = Recognised_devices_list.Union(lines_list).ToList(); } catch (ArgumentNullException) { }

            return Recognised_devices_list.ToArray();
        }

        private bool check_num_block()
        {
            Boolean Block = true;
            string[] lines = File.ReadAllLines(path);
            List<string> lines_list = new List<string>(lines);
            string full_string = lines_list.First(s => s.Contains("Number_blocks"));
            int n_blocks_i = lines_list.IndexOf(full_string);
            if (n_blocks_i >= 0)
            {
                char[] delChar = { '=' };
                int n_blocks = int.Parse(lines_list[n_blocks_i].Split(delChar)[1]);
                if (n_blocks >= maxBlocks)
                {
                    Block = false; //don't block the computer
                    reset = false;
                    this.EventLog.WriteEntry("[USBBlocker] Not blocking because maximun_block number exceeded", EventLogEntryType.Information);
                }
            }
            return Block;
        }

        private void BlockComputer()
        {
            //increment number of times blocked
            Increment_num_block();
            //Update last time blocked
            epoch_old = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            //dont reset the blocking counter
            reset = false;

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

        private void Initialize_File(string path)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(path))
                {
                    sw.WriteLine("Train_mode=True");
                    sw.WriteLine("Number_blocks=0");
                }
                this.EventLog.WriteEntry(String.Concat("The file has been generated ", path), EventLogEntryType.Information);
            }
            catch (Exception e)
            {
                this.EventLog.WriteEntry(String.Concat("The file cannot be generated", path, " because ", e.ToString()), EventLogEntryType.Information);
            }
        }

        //increment the Number_blocks variable and save it. If reset is true, the counter is set to 0.
        private void Increment_num_block()
        {
            string[] lines = File.ReadAllLines(path);
            List<string> lines_list = new List<string>(lines);
            string full_string = lines_list.First(s => s.Contains("Number_blocks"));
            int n_blocks_i = lines_list.IndexOf(full_string);
            if (n_blocks_i >= 0)
            {
                long epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                char[] delChar = { '=' };
                int n_blocks = int.Parse(lines_list[n_blocks_i].Split(delChar)[1]);

                //to detect false positives because it receives more events than plugged in devices
                //difference between an event and another has to be 2sec min to increment Number_blocks
                long secs_diff = epoch - epoch_old;

                if (secs_diff > min_secs)
                {
                    this.EventLog.WriteEntry(String.Concat("[USBBlocker] Incrementing Number_blocks to ", n_blocks + 1), EventLogEntryType.Warning);
                    lines_list[n_blocks_i] = string.Format("Number_blocks={0}", n_blocks + 1);
                    File.WriteAllLines(path, lines_list);
                }
            }
        }

        private void Reset_num_block()
        {
            string[] lines = File.ReadAllLines(path);
            List<string> lines_list = new List<string>(lines);
            string full_string = lines_list.First(s => s.Contains("Number_blocks"));
            int n_blocks_i = lines_list.IndexOf(full_string);
            if (n_blocks_i >= 0)
            {
                this.EventLog.WriteEntry(String.Concat("[USBBlocker] Reset number_blocks"), EventLogEntryType.Error);
                lines_list[n_blocks_i] = "Number_blocks=0";
                File.WriteAllLines(path, lines_list);
            }
        }
    }
}
