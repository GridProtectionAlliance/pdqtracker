//******************************************************************************************************
//  Main.cs - Gbtc
//
//  Copyright © 2010, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/eclipse-1.0.php
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  09/24/2010 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Setup
{
    public partial class Main : Form
    {
        // PDQTracker product code, as defined in the setup packages
        private const string ProductCode = "{FA7DBEF0-F7B6-4083-8146-88DCCEE4B97F}";

        private enum SetupType
        {
            Install,
            Uninstall
        }

        public Main()
        {
            InitializeComponent();
        }

        private void Main_Load(object sender, EventArgs e)
        {
            try
            {
                Version version = Assembly.GetEntryAssembly().GetName().Version;
                labelVersion.Text = string.Format(labelVersion.Text, version.Major, version.Minor, version.Build);
            }
            catch
            {
                labelVersion.Visible = false;
            }
        }

        private void buttonInstall_Click(object sender, EventArgs e)
        {
            bool runSetup = false;

            // Verify that .NET 4.6 is installed
            try
            {
                RegistryKey net46 = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\.NETFramework\\v4.0.30319\\SKUs\\.NETFramework,Version=v4.6");

                if (net46 == null)
                {
                    if (MessageBox.Show("Microsoft .NET 4.6 does not appear to be installed on this computer. The .NET 4.6 framework is required to be installed before you continue installation. Would you like to install it now?", ".NET 4.6 Check", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        Process net45Install;
                        const string netInstallPath = "Installers\\NDP46-KB3045557-x86-x64-AllOS-ENU.exe";

                        if (File.Exists(netInstallPath))
                        {
                            try
                            {
                                // Attempt to launch .NET 4.6 installer...
                                net45Install = new Process();
                                net45Install.StartInfo.FileName = netInstallPath;
                                net45Install.StartInfo.UseShellExecute = false;
                                net45Install.Start();
                            }
                            catch
                            {
                                // At a minimum open folder containing .NET 4.6 installer since its available to run...
                                net45Install = new Process();
                                net45Install.StartInfo.FileName = Directory.GetCurrentDirectory() + "\\Installers\\";
                                net45Install.StartInfo.UseShellExecute = true;
                                net45Install.Start();
                            }
                        }
                        else
                        {
                            net45Install = new Process();
                            net45Install.StartInfo.FileName = "https://www.microsoft.com/en-us/download/details.aspx?id=48130";
                            net45Install.StartInfo.UseShellExecute = true;
                            net45Install.Start();
                        }
                    }
                    else
                        runSetup = (MessageBox.Show("Would you like to attempt installation anyway?", ".NET 4.6 Check", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
                }
                else
                    runSetup = true;
            }
            catch
            {
                runSetup = (MessageBox.Show("The setup program was not able to determine if Microsoft .NET 4.6 is installed on this computer. The .NET 4.6 framework is required to be installed before you continue installation. Would you like to attempt installation anyway?", ".NET 4.6 Check", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
            }

            // See if an existing version is currently installed
            RegistryKey PDQTrackerInstallKey;

            PDQTrackerInstallKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + ProductCode);

            // If key wasn't found, test for 32-bit virtualized location
            if (PDQTrackerInstallKey == null)
                PDQTrackerInstallKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\" + ProductCode);

            if (PDQTrackerInstallKey != null)
            {
                if (MessageBox.Show("An existing version of the PDQTracker is installed on this computer. Would you like to remove the existing version?\r\n\r\nCurrent configuration will be preserved.", "Previous Version Check", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    runSetup = RunSetup(SetupType.Uninstall, false);
                else
                    runSetup = (MessageBox.Show("Would you like to attempt installation anyway?", "Previous Version Check", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);

                PDQTrackerInstallKey.Close();
            }

            if (runSetup)
                RunSetup(SetupType.Install, true);
        }

        private void buttonUninstall_Click(object sender, EventArgs e)
        {
            RunSetup(SetupType.Uninstall, false);
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private bool RunSetup(SetupType type, bool closeOnSuccess)
        {
            this.WindowState = FormWindowState.Minimized;

            // Install or uninstall PDQTracker
            Process PDQTrackerInstall = new Process();

            PDQTrackerInstall.StartInfo.FileName = "msiexec.exe";

            if (type == SetupType.Uninstall)
            {
                // Attempt to shutdown processes before uninstall
                AttemptToStopKeyProcesses();

                // Uninstall any version of the PDQTracker
                PDQTrackerInstall.StartInfo.Arguments = "/x " + ProductCode + " /qr";
            }
            else
            {
                // Install current version of the PDQTracker
                PDQTrackerInstall.StartInfo.Arguments = "/i Installers\\PDQTrackerSetup.msi";
            }

            PDQTrackerInstall.StartInfo.UseShellExecute = false;
            PDQTrackerInstall.StartInfo.CreateNoWindow = true;
            PDQTrackerInstall.Start();
            PDQTrackerInstall.WaitForExit();

            if (PDQTrackerInstall.ExitCode == 0)
            {
                // Run configuration setup utility post installation of PDQTracker, but not for uninstalls
                if (type == SetupType.Install)
                {
                    // Read registry installation parameters
                    string installPath, targetBitSize;

                    // Read values from primary registry location
                    installPath = AddPathSuffix(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Grid Protection Alliance\PDQTracker", "InstallPath", "").ToString().Trim());
                    targetBitSize = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Grid Protection Alliance\PDQTracker", "TargetBitSize", "64bit").ToString().Trim();

                    try
                    {
                        // Run configuration setup utility
                        Process configSetupUtility = new Process();

                        configSetupUtility.StartInfo.FileName = installPath + "ConfigurationSetupUtility.exe";
                        configSetupUtility.StartInfo.Arguments = "-install -" + targetBitSize;
                        configSetupUtility.StartInfo.UseShellExecute = false;
                        configSetupUtility.Start();
                        configSetupUtility.WaitForExit();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Setup program was not able to launch the PDQTracker Configuration Setup Utility due to an exception. You will need to run this program manually before starting the PDQTracker.\r\n\r\nError: " + ex.Message, "Configuration Setup Utility Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                // Install or uninstall PMU Connection Tester
                if (checkBoxConnectionTester.Checked)
                {
                    Process connectionTesterInstall = new Process();

                    connectionTesterInstall.StartInfo.FileName = "msiexec.exe";

                    if (type == SetupType.Uninstall)
                    {
                        // Uninstall any version of the PMU Connection Tester
                        connectionTesterInstall.StartInfo.Arguments = "/x Installers\\PMUConnectionTesterSetup64.msi /passive";
                    }
                    else
                    {
                        // Install current version of the PMU Connection Tester
                        connectionTesterInstall.StartInfo.Arguments = "/i Installers\\PMUConnectionTesterSetup64.msi";
                    }

                    connectionTesterInstall.StartInfo.UseShellExecute = false;
                    connectionTesterInstall.StartInfo.CreateNoWindow = true;
                    connectionTesterInstall.Start();
                    connectionTesterInstall.WaitForExit();

                    if (closeOnSuccess)
                        this.Close();
                    else
                        this.WindowState = FormWindowState.Normal;

                    return true;
                }

                if (closeOnSuccess)
                    this.Close();
                else
                    this.WindowState = FormWindowState.Normal;

                return true;
            }

            this.WindowState = FormWindowState.Normal;
            return false;
        }

        private void tabControlMain_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Load release notes
            if (tabControlMain.SelectedTab == tabPageReleaseNotes && richTextBoxReleaseNotes.TextLength == 0)
            {
                if (File.Exists("Help\\ReleaseNotes.rtf"))
                    richTextBoxReleaseNotes.LoadFile("Help\\ReleaseNotes.rtf");
                else
                    richTextBoxReleaseNotes.Text = "ERROR: Release notes file not found.";
            }
        }

        private void richTextBoxReleaseNotes_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            Process.Start("Explorer.exe", e.LinkText);
        }

        /// <summary>
        /// Makes sure path is suffixed with standard <see cref="Path.DirectorySeparatorChar"/>.
        /// </summary>
        /// <param name="filePath">The file path to be suffixed.</param>
        /// <returns>Suffixed path.</returns>
        private string AddPathSuffix(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = Path.DirectorySeparatorChar.ToString();
            }
            else
            {
                char suffixChar = filePath[filePath.Length - 1];

                if (suffixChar != Path.DirectorySeparatorChar && suffixChar != Path.AltDirectorySeparatorChar)
                    filePath += Path.DirectorySeparatorChar;
            }

            return filePath;
        }

        // Attempt to stop key processes/services before uninstall
        private void AttemptToStopKeyProcesses()
        {
            try
            {
                Process[] instances = Process.GetProcessesByName("PDQTrackerManager");

                if (instances.Length > 0)
                {
                    // Terminate all instances of PDQTracker Manager running on the local computer
                    foreach (Process process in instances)
                    {
                        process.Kill();
                    }
                }
            }
            catch
            {
            }

            // Attempt to access service controller for the PDQTracker
            ServiceController PDQTrackerServiceController = null;

            try
            {
                foreach (ServiceController service in ServiceController.GetServices())
                {
                    if (string.Compare(service.ServiceName, "PDQTracker", true) == 0)
                    {
                        PDQTrackerServiceController = service;
                        break;
                    }
                }
            }
            catch
            {
            }

            if (PDQTrackerServiceController != null)
            {
                try
                {
                    if (PDQTrackerServiceController.Status == ServiceControllerStatus.Running)
                    {
                        PDQTrackerServiceController.Stop();

                        // Can't wait forever for service to stop, so we time-out after 20 seconds
                        PDQTrackerServiceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20.0D));
                    }
                }
                catch
                {
                }
            }

            // If the PDQTracker service failed to stop or it is installed as stand-alone debug application, we try to stop any remaining running instances
            try
            {
                Process[] instances = Process.GetProcessesByName("PDQTracker");

                if (instances.Length > 0)
                {
                    // Terminate all instances of PDQTracker running on the local computer
                    foreach (Process process in instances)
                    {
                        process.Kill();
                    }
                }
            }
            catch
            {
            }

            // If uninstalling the PMU Connection Tester, we try to stop any running instances
            if (checkBoxConnectionTester.Checked)
            {
                try
                {
                    Process[] instances = Process.GetProcessesByName("PMUConnectionTester");

                    if (instances.Length > 0)
                    {
                        // Terminate all instances of PMU Connection Tester running on the local computer
                        foreach (Process process in instances)
                        {
                            process.Kill();
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }
}
