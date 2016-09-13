//  Copyright 2015 Jhorman Duban Rodríguez Pulgarín
//  
//  This file is part of InkAlert.
//  
//  InkAlert is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//  
//  InkAlert is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//  
//  You should have received a copy of the GNU General Public License
//  along with InkAlert.  If not, see <http://www.gnu.org/licenses/>.
//  
//  Jhorman Duban Rodríguez., hereby disclaims all copyright interest in 
//  the program "InkAlert" (which makes passes at 
//  compilers) written by Jhorman Duban Rodríguez.
//  
//  Jhorman Duban Rodríguez,
//  5 January 2016
//  For more information, visit <http://www.codigoinnovador.com/projects/inkalert/>

using System;
using System.Net.NetworkInformation;
using System.Windows.Forms;
using System.Management;
using System.IO;
using System.Printing;

namespace PrinterWakeUp
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //Application.Exit();
            //Hide Form
            this.Hide();

            //Update running configuration
            GlobalSetup.UpdateRunningConfig();
            this.timerPrinterWakeUp.Enabled = true;
            this.timerPrinterWakeUp.Interval = 1000;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Application.Exit();
            //File.Delete("PrinterConfigurationChecker.exe");
        }

        private bool pingIp(string printerIp)
        {
            if (printerIp == string.Empty || printerIp == "0.0.0.0")
            {
                MessageBox.Show("Dirección IP no válida", "Dirección ip no válida", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            try
            {
                int timeout = 120;
                Ping pingSender = new Ping();

                PingReply reply = pingSender.Send(printerIp, timeout);

                if (reply.Status == IPStatus.Success)
                {
                    return true;
                }

                return false;

            }
            catch (Exception ee)
            {
                EventLogger.LogEvent(this, ee.Message.ToString(), ee);
                return false;
            }
        }
        
        private void timerPrinterWakeUp_Tick(object sender, EventArgs e)
        {

            try
            {
                

                var printerQuery = new ManagementObjectSearcher("SELECT * from Win32_Printer");

                string[] printerStatus = { "Other", "Unknown", "Idle", "Printing", "WarmUp", "Stopped Printing", "Offline" };

                string[] printerState = {"Paused","Error","Pending Deletion","Paper Jam","Paper Out","Manual Feed","Paper Problem", "Offline","IO Active","Busy","Printing",
            "Output Bin Full","Not Available","Waiting", "Processing","Initialization","Warming Up","Toner Low","No Toner","Page Punt", "User Intervention Required",
            "Out of Memory","Door Open","Server_Unknown","Power Save"};

                foreach (var printer in printerQuery.Get())
                {
                    var shareName = printer.GetPropertyValue("ShareName");
                    var isDefault = printer.GetPropertyValue("Default");
                    var isOffline = printer.GetPropertyValue("WorkOffline");
                    var serverName = (string)printer.GetPropertyValue("ServerName");
                    var queueName = (string)printer.GetPropertyValue("Name");

                    if (serverName != null)
                    {
                        serverName = serverName.Replace("\\", "");
                    }else
                    {
                        continue;
                    }

                    if (!pingIp(serverName))
                    {
                        EventLogger.LogEvent(this, "El servidor de impresión (PC) " + serverName + " no está disponible en la red", null);
                        continue;
                    }

                    //foreach (PropertyData property in printer.Properties)
                    //{
                    //    Console.WriteLine(string.Format("{0}: {1}", property.Name, property.Value));
                    //}

                    //Application.Exit();

                    if (shareName != null && serverName != null && ((String)shareName).ToUpper().Contains("NO-USAR-"))
                    {
                        try
                        {
                            string printerName = (String)shareName;

                            PrintServer remotePrintServer = new PrintServer(@"\\" + serverName);
                            PrintQueueCollection queueCollection = remotePrintServer.GetPrintQueues();
                            PrintQueue printQueue = null;

                            foreach (PrintQueue pq in queueCollection)
                            {
                                if(pq.FullName.ToLower().Contains("pdf") ||
                                   pq.FullName.ToLower().Contains("fax") ||
                                   pq.FullName.ToLower().Contains("xps"))
                                {
                                    continue;
                                }

                                if (pq.FullName == queueName)
                                {
                                    printQueue = pq;

                                    if (printQueue.IsInError)
                                    {
                                        EventLogger.LogEvent(this, pq.FullName.ToString() + " reports to be in error state", null);
                                        continue;
                                    }

                                    if (printQueue.IsInitializing)
                                    {
                                        EventLogger.LogEvent(this, pq.FullName.ToString() + " reports to be initialising", null);
                                        continue;
                                    }

                                    if (printQueue.IsBusy)
                                    {
                                        EventLogger.LogEvent(this, pq.FullName.ToString() + " reports to be busy", null);
                                        continue;
                                    }

                                    if(printQueue.NumberOfJobs < 5)
                                    {
                                        File.Copy(Application.StartupPath + @"\SamsungPrinter\PrinterWakeup.prn", @"\\" + serverName + @"\" + shareName, true);
                                    }
                                    else
                                    {
                                        pq.Purge();
                                        EventLogger.LogEvent(this, pq.FullName.ToString() + " has more than 4 jobs pending. Printqueue was purged.", null);
                                        continue;
                                    }
                                 }
                            }
                        }
                        catch (Exception eee)
                        {
                            EventLogger.LogEvent(this, eee.Message.ToString(), eee);
                            continue;
                        }
                        
                    }
                }
            }
            catch (Exception ee)
            {
                EventLogger.LogEvent(this, ee.Message.ToString(), ee);
            }

            this.timerPrinterWakeUp.Interval = GlobalSetup.PrinterWakeUpInterval * 1000;

        }
    }
}
