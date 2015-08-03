//******************************************************************************************************
//  StatusUserControl.xaml.cs - Gbtc
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
//  11/09/2014 - Jeff Walker
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Serialization;
using GSF;
using GSF.Communication;
using GSF.Console;
using GSF.Data;
using GSF.Historian;
using GSF.Historian.Files;
using GSF.Identity;
using GSF.IO;
using GSF.Reflection;
using GSF.ServiceProcess;
using GSF.TimeSeries.UI;

#pragma warning disable 612,618

namespace PDQTrackerManager
{
    /// <summary>
    /// Interaction logic for StatusUserControl.xaml
    /// </summary>
    public partial class StatusUserControl : UserControl
    {

        private class DeviceStats
        {
            public string Name;
            public double[] MeasurementsReceived;
            public double[] MeasurementsExpected;
        }

        private class DeviceSignals
        {
            public string Name;
            public double[] Latched;
            public double[] Unreasonable;
        }


        #region [ Members ]
        private const int ReportDays = 32;  // Gather data for 32 days - necessary for 2-day history summary
        private const int Month = 30;       // 1 month = 30 days for reporting purposes
        private const int ReportPeriod = 2; // 2 Days of displayed data
        private double m_level4Threshold;
        private double m_level3Threshold;

        private List<DeviceStats> m_deviceStatsList;
        private List<DeviceSignals> m_deviceSignalsList;

        // Fields
        private readonly ObservableCollection<MenuDataItem> m_menuDataItems;
        private WindowsServiceClient m_windowsServiceClient;
        private DispatcherTimer m_refreshTimer;

        // Subscription fields
        private bool m_eventHandlerRegistered;
        //private Arguments args;

        #endregion

        #region [ Constructor ]

        /// <summary>
        /// Creates an instance of <see cref="StatusUserControl"/>.
        /// </summary>
        public StatusUserControl()
        {
            InitializeComponent();
            this.Loaded += StatusUserControl_Loaded;
            this.Unloaded += StatusUserControl_Unloaded;

            // Load Menu
            XmlRootAttribute xmlRootAttribute = new XmlRootAttribute("MenuDataItems");
            XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<MenuDataItem>), xmlRootAttribute);

            using (XmlReader reader = XmlReader.Create(FilePath.GetAbsolutePath("Menu.xml")))
            {
                m_menuDataItems = (ObservableCollection<MenuDataItem>)serializer.Deserialize(reader);
            }

            m_level4Threshold = 99.0D;
            m_level3Threshold = 90.0D;
        }

        #endregion

        #region [ Methods ]

        private void StatusUserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (m_windowsServiceClient != null && m_windowsServiceClient.Helper != null)
                {
                    m_windowsServiceClient.Helper.ReceivedServiceResponse -= Helper_ReceivedServiceResponse;
                }

                if (m_refreshTimer != null)
                    m_refreshTimer.Stop();
            }
            finally
            {
                m_refreshTimer = null;
            }
        }

        private void StatusUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!CommonFunctions.CurrentPrincipal.IsInRole("Administrator"))
                ButtonRestart.IsEnabled = false;

            if (!CommonFunctions.CurrentPrincipal.IsInRole("Administrator,Editor"))
                ButtonInputWizard.IsEnabled = false;

            m_windowsServiceClient = CommonFunctions.GetWindowsServiceClient();

            if (m_windowsServiceClient == null || m_windowsServiceClient.Helper.RemotingClient.CurrentState != ClientState.Connected)
            {
                ButtonRestart.IsEnabled = false;
            }
            else
            {
                m_windowsServiceClient.Helper.ReceivedServiceResponse += Helper_ReceivedServiceResponse;
                CommonFunctions.SendCommandToService("Version -actionable");
                CommonFunctions.SendCommandToService("Time -actionable");
                CommonFunctions.SendCommandToService("ReportingConfig");
                m_eventHandlerRegistered = true;
            }

            m_refreshTimer = new DispatcherTimer();
            m_refreshTimer.Interval = TimeSpan.FromSeconds(5);
            m_refreshTimer.Tick += RefreshTimer_Tick;
            m_refreshTimer.Start();

            if (IntPtr.Size == 8)
                TextBlockInstance.Text = "64-bit";
            else
                TextBlockInstance.Text = "32-bit";

            TextBlockLocalTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            Version appVersion = AssemblyInfo.EntryAssembly.Version;
            TextBlockManagerVersion.Text = appVersion.Major + "." + appVersion.Minor + "." + appVersion.Build + ".0";

            try
            {
                using (AdoDataConnection database = new AdoDataConnection(CommonFunctions.DefaultSettingsCategory))
                {
                    TextBlockDatabaseType.Text = database.DatabaseType.ToString();

                    try
                    {
                        if (database.IsSqlite || database.IsJetEngine)
                        {
                            // Extract database file name from connection string for file centric databases
                            TextBlockDatabaseName.Text = FilePath.GetFileName(database.Connection.ConnectionString.ParseKeyValuePairs()["Data Source"]);
                        }
                        else if (database.IsOracle)
                        {
                            // Extract user name from connection string for Oracle databases
                            TextBlockDatabaseName.Text = database.Connection.ConnectionString.ParseKeyValuePairs()["User Id"];
                        }
                        else
                        {
                            TextBlockDatabaseName.Text = database.Connection.Database;
                        }
                    }
                    catch
                    {
                        // Fall back on database name if file anything fails
                        TextBlockDatabaseName.Text = database.Connection.Database;
                    }
                }
            }
            catch
            {
                TextBlockDatabaseName.Text = "Not Available";
            }

            try
            {
                using (UserInfo info = new UserInfo(CommonFunctions.CurrentUser))
                {
                    if (info.Exists)
                        TextBlockUser.Text = info.LoginID;
                    else
                        TextBlockUser.Text = CommonFunctions.CurrentUser;
                }
            }
            catch
            {
                TextBlockUser.Text = CommonFunctions.CurrentUser;
            }

            CreateGridTextItem(0, 1, HorizontalAlignment.Right, (DateTime.Today - TimeSpan.FromDays(1)).ToString("MM/dd"), CompletenessGrid);
            CreateGridTextItem(0, 2, HorizontalAlignment.Right, DateTime.Now.ToString("MM/dd"), CompletenessGrid);

            CreateGridTextItem(1, 0, HorizontalAlignment.Left, "L4: Good", CompletenessGrid);
            CreateGridTextItem(2, 0, HorizontalAlignment.Left, "L3: Fair", CompletenessGrid);
            CreateGridTextItem(3, 0, HorizontalAlignment.Left, "L2: Poor", CompletenessGrid);
            CreateGridTextItem(4, 0, HorizontalAlignment.Left, "L1: Offline", CompletenessGrid);
            CreateGridTextItem(5, 0, HorizontalAlignment.Left, "L0: Failed", CompletenessGrid);
            CreateGridTextItem(6, 0, HorizontalAlignment.Left, "", CompletenessGrid);
            CreateGridTextItem(7, 0, HorizontalAlignment.Left, "Total", CompletenessGrid);

            CreateGridTextItem(0, 1, HorizontalAlignment.Right, (DateTime.Today - TimeSpan.FromDays(1)).ToString("MM/dd"), CorrectnesssGrid);
            CreateGridTextItem(0, 2, HorizontalAlignment.Right, DateTime.Now.ToString("MM/dd"), CorrectnesssGrid);

            CreateGridTextItem(1, 0, HorizontalAlignment.Left, "Good", CorrectnesssGrid);
            CreateGridTextItem(2, 0, HorizontalAlignment.Left, "Latched", CorrectnesssGrid);
            CreateGridTextItem(3, 0, HorizontalAlignment.Left, "Unreasonable", CorrectnesssGrid);
        }

        void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (m_windowsServiceClient != null && m_windowsServiceClient.Helper != null &&
                m_windowsServiceClient.Helper.RemotingClient != null && m_windowsServiceClient.Helper.RemotingClient.CurrentState == ClientState.Connected)
            {
                try
                {
                    ButtonRestart.IsEnabled = CommonFunctions.CurrentPrincipal.IsInRole("Administrator");
                    ButtonInputWizard.IsEnabled = CommonFunctions.CurrentPrincipal.IsInRole("Administrator,Editor");

                    if (!m_eventHandlerRegistered)
                    {
                        m_windowsServiceClient.Helper.ReceivedServiceResponse += Helper_ReceivedServiceResponse;
                        CommonFunctions.SendCommandToService("Version -actionable");
                        m_eventHandlerRegistered = true;
                    }

                    CommonFunctions.SendCommandToService("Time -actionable");
                }
                catch
                {
                }
            }
            else
            {
                ButtonRestart.IsEnabled = false;
            }

            TextBlockLocalTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        private void CreateGridTextItem(int row, int column, HorizontalAlignment align, string value, Grid targetGrid)
        {
            TextBlock textBlock = new TextBlock();
            textBlock.Text = value;
            textBlock.FontSize = 20;
            textBlock.HorizontalAlignment = align;
            textBlock.VerticalAlignment = VerticalAlignment.Center;
            textBlock.SetValue(Grid.ColumnProperty, column);
            textBlock.SetValue(Grid.RowProperty, row);

            targetGrid.Children.Add(textBlock);
        }

        private void ButtonQuickLink_Click(object sender, RoutedEventArgs e)
        {
            MenuDataItem item = new MenuDataItem();
            string stringToMatch = ((Button)sender).Tag.ToString();

            if (!string.IsNullOrEmpty(stringToMatch))
            {
                if (stringToMatch == "Restart")
                {
                    RestartService();
                }
                else
                {
                    GetMenuDataItem(m_menuDataItems, stringToMatch, ref item);

                    if ((object)item.MenuText != null)
                        item.Command.Execute(null);
                }
            }
        }

        /// <summary>
        /// Recursively finds menu item to navigate to when a button is clicked on the UI.
        /// </summary>
        /// <param name="items">Collection of menu items.</param>
        /// <param name="stringToMatch">Item to search for in menu items collection.</param>
        /// <param name="item">Returns a menu item.</param>
        private void GetMenuDataItem(ObservableCollection<MenuDataItem> items, string stringToMatch, ref MenuDataItem item)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].UserControlPath.ToLower() == stringToMatch.ToLower())
                {
                    item = items[i];
                    break;
                }
                else
                {
                    if (items[i].SubMenuItems.Count > 0)
                    {
                        GetMenuDataItem(items[i].SubMenuItems, stringToMatch, ref item);
                    }
                }
            }
        }

        private void RestartService()
        {
            try
            {
                if (MessageBox.Show("Do you want to restart service?", "Restart Service", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    CommonFunctions.SendCommandToService("Restart");
                    MessageBox.Show("Successfully sent RESTART command to the service.", "Restart Service", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch
            {
                MessageBox.Show("Failed sent RESTART command to the service." + Environment.NewLine + "Service is either offline or disconnected.", "Restart Service", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles ReceivedServiceResponse event.
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Helper_ReceivedServiceResponse(object sender, EventArgs<ServiceResponse> e)
        {
            string sourceCommand;
            bool responseSuccess;

            if (ClientHelper.TryParseActionableResponse(e.Argument, out sourceCommand, out responseSuccess))
            {
                if (sourceCommand.ToLower() == "version")
                {
                    this.Dispatcher.BeginInvoke((Action)delegate
                    {
                        TextBlockVersion.Text = e.Argument.Message.Substring(e.Argument.Message.ToLower().LastIndexOf("version:", StringComparison.Ordinal) + 8).Trim();
                    });
                }
                else if (sourceCommand.ToLower() == "time")
                {
                    this.Dispatcher.BeginInvoke((Action)delegate
                    {
                        string[] times = Regex.Split(e.Argument.Message, "\r\n");
                        if (times.Any())
                        {
                            string[] currentTimes = Regex.Split(times[0], ",");
                            if (currentTimes.Any())
                                TextBlockServerTime.Text = currentTimes[0].Substring(currentTimes[0].ToLower().LastIndexOf("system time:", StringComparison.Ordinal) + 12).Trim();
                        }
                    });
                }
                else if (sourceCommand.Trim().ToLower() == "reportingconfig")
                {
                    new Thread(() =>
                    {
                        string config = e.Argument.Message.TrimEnd();
                        Arguments args = new Arguments(config);

                        if (!double.TryParse(args["level4threshold"], out m_level4Threshold))
                            m_level4Threshold = 99.0D;

                        if (!double.TryParse(args["level3threshold"], out m_level3Threshold))
                            m_level3Threshold = 90.0D;

                        ReadCompletenessData();
                        ReadCorrectnessData();
                    }).Start();
                }
            }
        }

        private string GetDeviceName(MetadataRecord record)
        {
            string signalReference = record.Synonym1;
            return signalReference.Remove(signalReference.LastIndexOf('!')).Replace("LOCAL$", "");
        }

        private void ReadCompletenessData()
        {
            Dictionary<string, DeviceStats> deviceStatsLookup = new Dictionary<string, DeviceStats>();
            DateTime startTime;
            DateTime endTime;

            Dictionary<MetadataRecord, IEnumerable<IDataPoint>> measurementsReceived;
            Dictionary<MetadataRecord, IEnumerable<IDataPoint>> measurementsExpected;

            List<DeviceStats>[] todaysStats;
            List<DeviceStats>[] yesterdaysStats;

            string signalReference;
            string deviceName;
            int index;

            // Create the statistics reader for reading statistics from the archive
            using (StatisticsReader statisticsReader = new StatisticsReader())
            {
                endTime = DateTime.Today.AddDays(1.0D); // Tomorrow
                startTime = endTime - TimeSpan.FromDays(ReportDays);

                // Set up and open the statistics reader
                statisticsReader.StartTime = startTime;
                statisticsReader.EndTime = endTime;

                statisticsReader.ArchiveFilePath = FilePath.GetAbsolutePath("Statistics\\stat_archive.d");
                statisticsReader.Open();

                measurementsReceived = statisticsReader.Read("PMU", 4);
                measurementsExpected = statisticsReader.Read("PMU", 5);

                // Determine which devices in the archive have stats for both measurements received and measurements expected
                foreach (Tuple<MetadataRecord, MetadataRecord> tuple in measurementsReceived.Keys.Join(measurementsExpected.Keys, GetDeviceName, GetDeviceName, Tuple.Create))
                {
                    DeviceStats deviceStats;
                    signalReference = tuple.Item1.Synonym1;
                    deviceName = GetDeviceName(tuple.Item1);

                    // Ignore statistics that were calculated by an intermediate gateway
                    if (!signalReference.StartsWith("LOCAL$") && signalReference.Contains("LOCAL$"))
                        continue;

                    // Make sure LOCAL$ statistics take precedence over other statistics calculated for the same device
                    if (deviceStatsLookup.ContainsKey(deviceName) && !signalReference.StartsWith("LOCAL$"))
                        continue;

                    // Create arrays to hold the total sum of the stats for each day being reported
                    deviceStats = new DeviceStats()
                    {
                        Name = deviceName,
                        MeasurementsReceived = new double[ReportDays],
                        MeasurementsExpected = new double[ReportDays]
                    };

                    // Calculate the total measurements received for each day being reported
                    foreach (IDataPoint dataPoint in measurementsReceived[tuple.Item1])
                    {
                        index = (dataPoint.Time.ToDateTime() - startTime).Days;

                        if (index >= 0 && index < ReportDays)
                            deviceStats.MeasurementsReceived[index] += dataPoint.Value;
                    }

                    // Calculate the total measurements expected for each day being reported
                    foreach (IDataPoint dataPoint in measurementsExpected[tuple.Item2])
                    {
                        index = (dataPoint.Time.ToDateTime() - startTime).Days;

                        if (index >= 0 && index < ReportDays)
                            deviceStats.MeasurementsExpected[index] += dataPoint.Value;
                    }

                    // Store the calculated stats per device
                    deviceStatsLookup[deviceName] = deviceStats;
                }
            }

            // Store the statistics data to be used in the UI
            m_deviceStatsList = deviceStatsLookup.Values.ToList();

            todaysStats = GetLevels(ReportDays);
            yesterdaysStats = GetLevels(ReportDays - 1);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                int total = yesterdaysStats[0].Count + yesterdaysStats[1].Count + yesterdaysStats[2].Count + yesterdaysStats[3].Count + yesterdaysStats[4].Count;
                CreateGridTextItem(1, 1, HorizontalAlignment.Right, yesterdaysStats[4].Count.ToString(), CompletenessGrid);
                CreateGridTextItem(2, 1, HorizontalAlignment.Right, yesterdaysStats[3].Count.ToString(), CompletenessGrid);
                CreateGridTextItem(3, 1, HorizontalAlignment.Right, yesterdaysStats[2].Count.ToString(), CompletenessGrid);
                CreateGridTextItem(4, 1, HorizontalAlignment.Right, yesterdaysStats[1].Count.ToString(), CompletenessGrid);
                CreateGridTextItem(5, 1, HorizontalAlignment.Right, yesterdaysStats[0].Count.ToString(), CompletenessGrid);
                CreateGridTextItem(7, 1, HorizontalAlignment.Right, total.ToString(), CompletenessGrid);

                total = todaysStats[0].Count + todaysStats[1].Count + todaysStats[2].Count + todaysStats[3].Count + todaysStats[4].Count;
                CreateGridTextItem(1, 2, HorizontalAlignment.Right, todaysStats[4].Count.ToString(), CompletenessGrid);
                CreateGridTextItem(2, 2, HorizontalAlignment.Right, todaysStats[3].Count.ToString(), CompletenessGrid);
                CreateGridTextItem(3, 2, HorizontalAlignment.Right, todaysStats[2].Count.ToString(), CompletenessGrid);
                CreateGridTextItem(4, 2, HorizontalAlignment.Right, todaysStats[1].Count.ToString(), CompletenessGrid);
                CreateGridTextItem(5, 2, HorizontalAlignment.Right, todaysStats[0].Count.ToString(), CompletenessGrid);
                CreateGridTextItem(7, 2, HorizontalAlignment.Right, total.ToString(), CompletenessGrid);
            }));
        }

        private void ReadCorrectnessData()
        {
            DateTime startTime;
            DateTime endTime;

            Dictionary<MetadataRecord, IEnumerable<IDataPoint>> measurementsExpected;
            Dictionary<MetadataRecord, IEnumerable<IDataPoint>> measurementsReceived;
            Dictionary<MetadataRecord, IEnumerable<IDataPoint>> measurementsLatched;
            Dictionary<MetadataRecord, IEnumerable<IDataPoint>> measurementsUnreasonable;

            // Create the statistics reader for reading statistics from the archive
            using (StatisticsReader statisticsReader = new StatisticsReader())
            {
                endTime = DateTime.Today.AddDays(1.0D); // Tomorrow
                startTime = endTime - TimeSpan.FromDays(ReportPeriod);

                // Set up and open the statistics reader
                statisticsReader.StartTime = startTime;
                statisticsReader.EndTime = endTime;

                statisticsReader.ArchiveFilePath = FilePath.GetAbsolutePath("Statistics\\stat_archive.d");
                statisticsReader.Open();

                measurementsExpected = statisticsReader.Read("PMU", 5);
                measurementsReceived = statisticsReader.Read("PMU", 4);
                measurementsLatched = statisticsReader.Read(980);
                measurementsUnreasonable = statisticsReader.Read(900);

                float[] totalExpected =
                {
                    measurementsExpected
                        .SelectMany(kvp => kvp.Value)
                        .Where(dataPoint => dataPoint.Time.ToDateTime().Date == DateTime.Today.AddDays(-1.0D))
                        .Select(dataPoint => dataPoint.Value)
                        .DefaultIfEmpty(0.0F)
                        .Sum(),
                    measurementsExpected
                        .SelectMany(kvp => kvp.Value)
                        .Where(dataPoint => dataPoint.Time.ToDateTime().Date == DateTime.Today)
                        .Select(dataPoint => dataPoint.Value)
                        .DefaultIfEmpty(0.0F)
                        .Sum()
                };


                float[] totalReceived =
                {
                    measurementsReceived
                        .SelectMany(kvp => kvp.Value)
                        .Where(dataPoint => dataPoint.Time.ToDateTime().Date == DateTime.Today.AddDays(-1.0D))
                        .Select(dataPoint => dataPoint.Value)
                        .DefaultIfEmpty(0.0F)
                        .Sum(),
                    measurementsReceived
                        .SelectMany(kvp => kvp.Value)
                        .Where(dataPoint => dataPoint.Time.ToDateTime().Date == DateTime.Today)
                        .Select(dataPoint => dataPoint.Value)
                        .DefaultIfEmpty(0.0F)
                        .Sum()
                };

                float[] totalLatched =
                {
                    measurementsLatched
                        .SelectMany(kvp => kvp.Value)
                        .Where(dataPoint => dataPoint.Time.ToDateTime().Date == DateTime.Today.AddDays(-1.0D))
                        .Select(dataPoint => dataPoint.Value)
                        .DefaultIfEmpty(0.0F)
                        .Sum(),
                    measurementsLatched
                        .SelectMany(kvp => kvp.Value)
                        .Where(dataPoint => dataPoint.Time.ToDateTime().Date == DateTime.Today)
                        .Select(dataPoint => dataPoint.Value)
                        .DefaultIfEmpty(0.0F)
                        .Sum()
                };

                float[] totalUnreasonable =
                {
                    measurementsUnreasonable
                        .SelectMany(kvp => kvp.Value)
                        .Where(dataPoint => dataPoint.Time.ToDateTime().Date == DateTime.Today.AddDays(-1.0D))
                        .Select(dataPoint => dataPoint.Value)
                        .DefaultIfEmpty(0.0F)
                        .Sum(),
                    measurementsUnreasonable
                        .SelectMany(kvp => kvp.Value)
                        .Where(dataPoint => dataPoint.Time.ToDateTime().Date == DateTime.Today)
                        .Select(dataPoint => dataPoint.Value)
                        .DefaultIfEmpty(0.0F)
                        .Sum()
                };

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (totalExpected[0] == 0)
                    {
                        totalExpected[0] = 1;
                    } // Not allowed to divide by zero
                    CreateGridTextItem(1, 1, HorizontalAlignment.Right, ((totalReceived[0] - totalLatched[0] - totalUnreasonable[0]) / totalExpected[0]).ToString("0.00%"), CorrectnesssGrid);
                    CreateGridTextItem(2, 1, HorizontalAlignment.Right, (totalLatched[0] / totalExpected[0]).ToString("0.00%"), CorrectnesssGrid);
                    CreateGridTextItem(3, 1, HorizontalAlignment.Right, (totalUnreasonable[0] / totalExpected[0]).ToString("0.00%"), CorrectnesssGrid);
                    if (totalExpected[1] == 0)
                    {
                        totalExpected[1] = 1;
                    } // Not allowed to divide by zero
                    CreateGridTextItem(1, 2, HorizontalAlignment.Right, ((totalReceived[1] - totalLatched[1] - totalUnreasonable[1]) / totalExpected[1]).ToString("0.00%"), CorrectnesssGrid);
                    CreateGridTextItem(2, 2, HorizontalAlignment.Right, (totalLatched[1] / totalExpected[1]).ToString("0.00%"), CorrectnesssGrid);
                    CreateGridTextItem(3, 2, HorizontalAlignment.Right, (totalUnreasonable[1] / totalExpected[1]).ToString("0.00%"), CorrectnesssGrid);
                }));
            }
        }

        private List<DeviceStats>[] GetLevels(int reportDay)
        {
            return Enumerable.Range(0, 5)
                .Select(level => m_deviceStatsList.Where(devStats => GetLevel(devStats, reportDay) == level).ToList())
                .ToArray();
        }

        private int GetLevel(DeviceStats deviceStats, int reportDay)
        {
            int reportDayIndex = reportDay - 1;
            double measurementsReceived = deviceStats.MeasurementsReceived[reportDayIndex];
            double measurementsExpected = deviceStats.MeasurementsExpected[reportDayIndex];

            if (measurementsExpected == 0.0D)
                return -1;

            if (measurementsReceived >= (m_level4Threshold * measurementsExpected) / 100.0D)
                return 4;

            if (measurementsReceived >= (m_level3Threshold * measurementsExpected) / 100.0D)
                return 3;

            if (measurementsReceived > 0.0D)
                return 2;

            if (deviceStats.MeasurementsReceived.Skip(reportDay - Month).Take(ReportDays).Any(receivedStat => receivedStat > 0.0D))
                return 1;

            return 0;
        }

        #endregion
    }
}
