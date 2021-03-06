﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Globalization;
using System.Management;

namespace NiceHashMiner
{
    public partial class Form1 : Form
    {
        public static string[] MiningLocation = { "eu", "usa", "hk", "jp" };
        private static string VisitURL = "http://www.nicehash.com";
        public static NiceHashSMA[] NiceHashData = null;

        public static Miner[] Miners;

        private Timer MinerStatsCheck;
        private Timer UpdateCheck;
        private Timer SMACheck;
        private Timer BalanceCheck;
        private Timer SMAMinerCheck;
        private Timer BitcoinExchangeCheck;
        private Timer StartupTimer;
        private Timer IdleCheck;
        private Form3 LoadingScreen;
        private int LoadCounter = 0;
        private int TotalLoadSteps = 13;
        private int CPUs;
        private bool ShowWarningNiceHashData;

        private Random R;

        private double BitcoinRate;

        private Form2 BenchmarkForm;


        public Form1(bool ss)
        {
            InitializeComponent();

            label_ServiceLocation.Text = International.GetText("form1_service_loc") + ":";
            label_BitcoinAddress.Text = International.GetText("form1_btc_address") + ":";
            label_WorkerName.Text = International.GetText("form1_worker_name") + ":";

            linkLabel_VisitUs.Text = International.GetText("form1_visit_us");
            linkLabel_CheckStats.Text = International.GetText("form1_check_stats");
            linkLabel_ChooseBTCWallet.Text = International.GetText("form1_choose_bitcoin_wallet");

            label_RateCPU.Text = International.GetText("form1_rate") + ":";
            label_RateNVIDIA5X.Text = International.GetText("form1_rate") + ":";
            label_RateNVIDIA3X.Text = International.GetText("form1_rate") + ":";
            label_RateNVIDIA2X.Text = International.GetText("form1_rate") + ":";
            label_RateAMD.Text = International.GetText("form1_rate") + ":";

            label_RateCPUBTC.Text = "0.00000000 BTC/" + International.GetText("form1_rate_day");
            label_RateNVIDIA5XBTC.Text = "0.00000000 BTC/" + International.GetText("form1_rate_day");
            label_RateNVIDIA3XBTC.Text = "0.00000000 BTC/" + International.GetText("form1_rate_day");
            label_RateNVIDIA2XBTC.Text = "0.00000000 BTC/" + International.GetText("form1_rate_day");
            label_RateAMDBTC.Text = "0.00000000 BTC/" + International.GetText("form1_rate_day");

            label_RateCPUDollar.Text = "0.00 $/" + International.GetText("form1_rate_day");
            label_RateNVIDIA5XDollar.Text = "0.00 $/" + International.GetText("form1_rate_day");
            label_RateNVIDIA3XDollar.Text = "0.00 $/" + International.GetText("form1_rate_day");
            label_RateNVIDIA2XDollar.Text = "0.00 $/" + International.GetText("form1_rate_day");
            label_RateAMDDollar.Text = "0.00 $/" + International.GetText("form1_rate_day");

            toolStripStatusLabel_GlobalRate.Text = International.GetText("form1_global_rate") + ":";
            toolStripStatusLabel_BTCDay.Text = "BTC/" + International.GetText("form1_rate_day");
            toolStripStatusLabel_Balance.Text = "$/" + International.GetText("form1_rate_day") + "     " + International.GetText("form1_balance") + ":";

            listView1.Columns[0].Text = International.GetText("form1_listView_Columns0");
            listView1.Columns[1].Text = International.GetText("form1_listView_Columns1");
            listView1.Columns[2].Text = International.GetText("form1_listView_Columns2");

            buttonBenchmark.Text = International.GetText("form1_benchmark");
            buttonSettings.Text = International.GetText("form1_settings");
            buttonStartMining.Text = International.GetText("form1_start");
            buttonStopMining.Text = International.GetText("form1_stop");

            if (ss)
            {
                Form4 f4 = new Form4();
                f4.ShowDialog();
            }

            // Log the computer's amount of Total RAM and Page File Size
            ManagementObjectCollection moc = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_OperatingSystem").Get();
            foreach (ManagementObject mo in moc)
            {
                long TotalRam = long.Parse(mo["TotalVisibleMemorySize"].ToString()) / 1024;
                long PageFileSize = (long.Parse(mo["TotalVirtualMemorySize"].ToString()) / 1024) - TotalRam;
                Helpers.ConsolePrint("NICEHASH", "Total RAM: "      + TotalRam     + "MB");
                Helpers.ConsolePrint("NICEHASH", "Page File Size: " + PageFileSize + "MB");
            }

            R = new Random((int)DateTime.Now.Ticks);

            Text += " v" + Application.ProductVersion;

            if (Config.ConfigData.Location >= 0 && Config.ConfigData.Location < MiningLocation.Length)
                comboBox1.SelectedIndex = Config.ConfigData.Location;
            else
                comboBox1.SelectedIndex = 0;

            textBox1.Text = Config.ConfigData.BitcoinAddress;
            textBox2.Text = Config.ConfigData.WorkerName;
            ShowWarningNiceHashData = true;
        }


        private void AfterLoadComplete()
        {
            this.Enabled = true;

            if (Config.ConfigData.AutoStartMining)
            {
                button1_Click(null, null);
            }

            IdleCheck = new Timer();
            IdleCheck.Tick += IdleCheck_Tick;
            IdleCheck.Interval = 500;
            IdleCheck.Start();
        }


        private void IdleCheck_Tick(object sender, EventArgs e)
        {
            if (!Config.ConfigData.StartMiningWhenIdle) return;

            uint MSIdle = Helpers.GetIdleTime();

            if (MinerStatsCheck.Enabled)
            {
                if (MSIdle < (Config.ConfigData.MinIdleSeconds * 1000))
                {
                    button2_Click(null, null);
                    Helpers.ConsolePrint("NICEHASH", "Resumed from idling");
                }
            }
            else
            {
                if (BenchmarkForm == null && (MSIdle > (Config.ConfigData.MinIdleSeconds * 1000)))
                {
                    Helpers.ConsolePrint("NICEHASH", "Entering idling state");
                    button1_Click(null, null);
                }
            }
        }


        private void StartupTimer_Tick(object sender, EventArgs e)
        {
            StartupTimer.Stop();
            StartupTimer = null;

            // get all CPUs
            CPUs = CPUID.GetPhysicalProcessorCount();

            // get all cores (including virtual - HT can benefit mining)
            int ThreadsPerCPU = CPUID.GetVirtualCoresCount() / CPUs;

            if (!Helpers.InternalCheckIsWow64() && !Config.ConfigData.AutoStartMining)
            {
                MessageBox.Show(International.GetText("form1_msgbox_CPUMining64bitMsg"),
                                International.GetText("form1_msgbox_CPUMining64bitTitle"),
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                CPUs = 0;
            }

            if (ThreadsPerCPU * CPUs > 64)
            {
                MessageBox.Show(International.GetText("form1_msgbox_CPUMining64CoresMsg"),
                                International.GetText("form1_msgbox_CPUMining64CoresTitle"),
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                CPUs = 0;
            }

            int ThreadsPerCPUMask = ThreadsPerCPU;
            ThreadsPerCPU -= Config.ConfigData.LessThreads;
            if (ThreadsPerCPU < 1)
            {
                MessageBox.Show(International.GetText("form1_msgbox_CPUMiningLessThreadMsg"),
                                International.GetText("form1_msgbox_CPUMiningLessThreadTitle"),
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                CPUs = 0;
            }

            Miners = new Miner[CPUs + 4];

            if (CPUs == 1)
                Miners[0] = new cpuminer(0, ThreadsPerCPU, 0);
            else
            {
                for (int i = 0; i < CPUs; i++)
                    Miners[i] = new cpuminer(i, ThreadsPerCPU, CPUID.CreateAffinityMask(i, ThreadsPerCPUMask));
            }

            LoadingScreen.LoadText.Text = International.GetText("form1_loadtext_NVIDIA5X");
            IncreaseLoadCounter();

            Miners[CPUs] = new ccminer_sp();

            LoadingScreen.LoadText.Text = International.GetText("form1_loadtext_NVIDIA3X");
            IncreaseLoadCounter();
            
            Miners[CPUs + 1] = new ccminer_tpruvot();

            LoadingScreen.LoadText.Text = International.GetText("form1_loadtext_NVIDIA2X");
            IncreaseLoadCounter();

            Miners[CPUs + 2] = new ccminer_tpruvot_sm21();

            LoadingScreen.LoadText.Text = International.GetText("form1_loadtext_AMD");
            IncreaseLoadCounter();

            Miners[CPUs + 3] = new sgminer();

            for (int i = 0; i < CPUs; i++)
            {
                try
                {
                    if ((Miners[CPUs + 0].CDevs.Count > 0 || Miners[CPUs + 1].CDevs.Count > 0 ||
                         Miners[CPUs + 2].CDevs.Count > 0 || Miners[CPUs + 3].CDevs.Count > 0) && i < CPUs)
                    {
                        Miners[i].CDevs[0].Enabled = false;
                    }
                }
                catch { }
            }

            LoadingScreen.LoadText.Text = International.GetText("form1_loadtext_SaveConfig");
            IncreaseLoadCounter();
            
            for (int i = 0; i < Miners.Length; i++)
            {
                if (Config.ConfigData.Groups.Length > i)
                {
                    Miners[i].ExtraLaunchParameters = Config.ConfigData.Groups[i].ExtraLaunchParameters;
                    Miners[i].UsePassword = Config.ConfigData.Groups[i].UsePassword;
                    Miners[i].MinimumProfit = Config.ConfigData.Groups[i].MinimumProfit;
                    Miners[i].DaggerHashimotoGenerateDevice = Config.ConfigData.Groups[i].DaggerHashimotoGenerateDevice;
                    if (Config.ConfigData.Groups[i].APIBindPort > 0)
                        Miners[i].APIPort = Config.ConfigData.Groups[i].APIBindPort;
                    for (int z = 0; z < Config.ConfigData.Groups[i].Algorithms.Length && z < Miners[i].SupportedAlgorithms.Length; z++)
                    {
                        Miners[i].SupportedAlgorithms[z].BenchmarkSpeed = Config.ConfigData.Groups[i].Algorithms[z].BenchmarkSpeed;
                        Miners[i].SupportedAlgorithms[z].ExtraLaunchParameters = Config.ConfigData.Groups[i].Algorithms[z].ExtraLaunchParameters;
                        Miners[i].SupportedAlgorithms[z].UsePassword = Config.ConfigData.Groups[i].Algorithms[z].UsePassword;
                        Miners[i].SupportedAlgorithms[z].Skip = Config.ConfigData.Groups[i].Algorithms[z].Skip;

                        Miners[i].SupportedAlgorithms[z].DisabledDevice = new bool[Miners[i].CDevs.Count];
                        if (Config.ConfigData.Groups[i].Algorithms[z].DisabledDevices != null)
                        {
                            if (Config.ConfigData.Groups[i].Algorithms[z].DisabledDevices.Length < Miners[i].CDevs.Count)
                            {
                                for (int j = 0; j < Config.ConfigData.Groups[i].Algorithms[z].DisabledDevices.Length; j++)
                                    Miners[i].SupportedAlgorithms[z].DisabledDevice[j] = Config.ConfigData.Groups[i].Algorithms[z].DisabledDevices[j];
                                for (int j = Config.ConfigData.Groups[i].Algorithms[z].DisabledDevices.Length; j < Miners[i].CDevs.Count; j++)
                                    Miners[i].SupportedAlgorithms[z].DisabledDevice[j] = false;
                            }
                            else
                            {
                                for (int j = 0; j < Miners[i].CDevs.Count; j++)
                                    Miners[i].SupportedAlgorithms[z].DisabledDevice[j] = Config.ConfigData.Groups[i].Algorithms[z].DisabledDevices[j];
                            }
                        }
                        else
                        {
                            Miners[i].GetDisabledDevicePerAlgo();
                        }
                    }
                }
                else
                {
                    Miners[i].GetDisabledDevicePerAlgo();
                }
                for (int k = 0; k < Miners[i].CDevs.Count; k++)
                {
                    ComputeDevice D = Miners[i].CDevs[k];
                    if (Config.ConfigData.Groups.Length > i)
                    {
                        D.Enabled = true;
                        for (int z = 0; z < Config.ConfigData.Groups[i].DisabledDevices.Length; z++)
                        {
                            if (Config.ConfigData.Groups[i].DisabledDevices[z] == k)
                            {
                                D.Enabled = false;
                                break;
                            }
                        }
                    }
                    ListViewItem lvi = new ListViewItem();
                    lvi.SubItems.Add(D.Vendor);
                    lvi.SubItems.Add(D.Name);
                    lvi.Checked = D.Enabled;
                    lvi.Tag = D;
                    listView1.Items.Add(lvi);
                }
            }

            Config.RebuildGroups();

            LoadingScreen.LoadText.Text = International.GetText("form1_loadtext_CheckLatestVersion");
            IncreaseLoadCounter();

            MinerStatsCheck = new Timer();
            MinerStatsCheck.Tick += MinerStatsCheck_Tick;
            MinerStatsCheck.Interval = Config.ConfigData.MinerAPIQueryInterval * 1000;

            SMAMinerCheck = new Timer();
            SMAMinerCheck.Tick += SMAMinerCheck_Tick;
            SMAMinerCheck.Interval = Config.ConfigData.SwitchMinSecondsFixed * 1000 + R.Next(Config.ConfigData.SwitchMinSecondsDynamic * 1000);
            if (Miners[CPUs + 3].CDevs.Count > 0) SMACheck.Interval += (Config.ConfigData.SwitchMinSecondsAMD * 1000);

            UpdateCheck = new Timer();
            UpdateCheck.Tick += UpdateCheck_Tick;
            UpdateCheck.Interval = 1000 * 3600; // every 1 hour
            UpdateCheck.Start();
            UpdateCheck_Tick(null, null);

            LoadingScreen.LoadText.Text = International.GetText("form1_loadtext_GetNiceHashSMA");
            IncreaseLoadCounter();

            SMACheck = new Timer();
            SMACheck.Tick += SMACheck_Tick;
            SMACheck.Interval = 60 * 1000; // every 60 seconds
            SMACheck.Start();
            SMACheck_Tick(null, null);

            LoadingScreen.LoadText.Text = International.GetText("form1_loadtext_GetBTCRate");
            IncreaseLoadCounter();

            BitcoinExchangeCheck = new Timer();
            BitcoinExchangeCheck.Tick += BitcoinExchangeCheck_Tick;
            BitcoinExchangeCheck.Interval = 1000 * 3601; // every 1 hour and 1 second
            BitcoinExchangeCheck.Start();
            BitcoinExchangeCheck_Tick(null, null);

            LoadingScreen.LoadText.Text = International.GetText("form1_loadtext_GetNiceHashBalance");
            IncreaseLoadCounter();

            BalanceCheck = new Timer();
            BalanceCheck.Tick += BalanceCheck_Tick;
            BalanceCheck.Interval = 61 * 1000; // every 61 seconds
            BalanceCheck.Start();
            BalanceCheck_Tick(null, null);

            LoadingScreen.LoadText.Text = International.GetText("form1_loadtext_SetEnvironmentVariable");
            IncreaseLoadCounter();

            SetEnvironmentVariables();

            IncreaseLoadCounter();
            LoadingScreen.LoadText.Text = International.GetText("form1_loadtext_SetWindowsErrorReporting");
            Helpers.DisableWindowsErrorReporting(Config.ConfigData.DisableWindowsErrorReporting);

            IncreaseLoadCounter();
            if (Config.ConfigData.NVIDIAP0State)
            {
                LoadingScreen.LoadText.Text = International.GetText("form1_loadtext_NVIDIAP0State");
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = "nvidiasetp0state.exe";
                    psi.Verb = "runas";
                    psi.UseShellExecute = true;
                    psi.CreateNoWindow = true;
                    Process p = Process.Start(psi);
                    p.WaitForExit();
                    if (p.ExitCode != 0)
                        Helpers.ConsolePrint("NICEHASH", "nvidiasetp0state returned error code: " + p.ExitCode.ToString());
                    else
                        Helpers.ConsolePrint("NICEHASH", "nvidiasetp0state all OK");
                }
                catch (Exception ex)
                {
                    Helpers.ConsolePrint("NICEHASH", "nvidiasetp0state error: " + ex.Message);
                }
            }

            IncreaseLoadCounter();
        }


        private void Form1_Shown(object sender, EventArgs e)
        {
            LoadingScreen = new Form3();
            LoadingScreen.Location = new Point(this.Location.X + (this.Width - LoadingScreen.Width) / 2, this.Location.Y + (this.Height - LoadingScreen.Height) / 2);
            LoadingScreen.Show();
            LoadingScreen.progressBar1.Maximum = TotalLoadSteps;
            LoadingScreen.progressBar1.Value = 0;
            LoadingScreen.LoadText.Text = International.GetText("form1_loadtext_CPU");

            StartupTimer = new Timer();
            StartupTimer.Tick += StartupTimer_Tick;
            StartupTimer.Interval = 200;
            StartupTimer.Start();
        }


        private void IncreaseLoadCounter()
        {
            LoadCounter++;
            LoadingScreen.progressBar1.Value = LoadCounter;
            LoadingScreen.Update();
            if (LoadCounter >= TotalLoadSteps)
            {
                if (LoadingScreen != null)
                {
                    LoadingScreen.Close();
                    LoadingScreen = null;

                    AfterLoadComplete();
                }
            }
        }


        private void SMAMinerCheck_Tick(object sender, EventArgs e)
        {
            SMAMinerCheck.Interval = Config.ConfigData.SwitchMinSecondsFixed * 1000 + R.Next(Config.ConfigData.SwitchMinSecondsDynamic * 1000);
            if (Miners[CPUs + 3].CDevs.Count > 0) SMACheck.Interval += (Config.ConfigData.SwitchMinSecondsAMD * 1000);

            string Worker = textBox2.Text.Trim();
            if (Worker.Length > 0)
                Worker = textBox1.Text.Trim() + "." + Worker;
            else
                Worker = textBox1.Text.Trim();

            foreach (Miner m in Miners)
            {
                if (m.EnabledDeviceCount() == 0) continue;

                int MaxProfitIndex = m.GetMaxProfitIndex(NiceHashData);

                if (m.NotProfitable)
                {
                    m.Stop(false);
                    continue;
                }
                
                if (m.CurrentAlgo != MaxProfitIndex)
                {
                    if (m.CurrentAlgo >= 0)
                    {
                        m.Stop(true);
                        // wait 0.5 seconds before going on
                        System.Threading.Thread.Sleep(Config.ConfigData.MinerRestartDelayMS);
                    }
                }
                m.CurrentAlgo = MaxProfitIndex;

                m.Start(m.SupportedAlgorithms[MaxProfitIndex].NiceHashID,
                    "stratum+tcp://" + NiceHashData[m.SupportedAlgorithms[MaxProfitIndex].NiceHashID].name + "." + MiningLocation[comboBox1.SelectedIndex] + ".nicehash.com:" +
                    NiceHashData[m.SupportedAlgorithms[MaxProfitIndex].NiceHashID].port, Worker);
            }
        }


        private void MinerStatsCheck_Tick(object sender, EventArgs e)
        {
            string CPUAlgoName = "";
            double CPUTotalSpeed = 0;
            double CPUTotalRate = 0;

            // Reset all stats
            SetCPUStats("", 0, 0);
            SetNVIDIAtp21Stats("", 0, 0);
            SetNVIDIAspStats("", 0, 0);
            SetNVIDIAtpStats("", 0, 0);
            SetAMDOpenCLStats("", 0, 0);

            foreach (Miner m in Miners)
            {
                if (m.EnabledDeviceCount() == 0 || m.NotProfitable) continue;

                if (m is cpuminer && m.AlgoNameIs("hodl"))
                {
                    string pname = m.Path.Split('\\')[2];
                    pname = pname.Substring(0, pname.Length - 4);

                    Process[] processes = Process.GetProcessesByName(pname);

                    if (processes.Length < CPUID.GetPhysicalProcessorCount())
                        m.Restart();

                    int algoIndex = m.GetAlgoIndex("hodl");
                    CPUAlgoName = "hodl";
                    CPUTotalSpeed = m.SupportedAlgorithms[algoIndex].BenchmarkSpeed;
                    CPUTotalRate = NiceHashData[m.SupportedAlgorithms[algoIndex].NiceHashID].paying * CPUTotalSpeed * 0.000000001;

                    continue;
                }

                APIData AD = m.GetSummary();
                if (AD == null)
                {
                    // Make sure sgminer has time to start
                    // properly on slow CPU system
                    if (m.StartingUpDelay && m.NumRetries > 0)
                    {
                        m.NumRetries--;
                        if (m.NumRetries == 0) m.StartingUpDelay = false;
                        Helpers.ConsolePrint(m.MinerDeviceName, "NumRetries: " + m.NumRetries);
                        continue;
                    }

                    if (m is sgminer && m.NumRetries > 0)
                    {
                        m.NumRetries--;
                        continue;
                    }

                    // API is inaccessible, try to restart miner
                    m.Restart();

                    continue;
                }
                else
                    m.StartingUpDelay = false;

                if (NiceHashData != null)
                    m.CurrentRate = NiceHashData[AD.AlgorithmID].paying * AD.Speed * 0.000000001;

                if (m is cpuminer)
                {
                    CPUAlgoName = AD.AlgorithmName;
                    CPUTotalSpeed += AD.Speed;
                    CPUTotalRate += m.CurrentRate;
                }
                else if (m is ccminer_tpruvot_sm21)
                {
                    SetNVIDIAtp21Stats(AD.AlgorithmName, AD.Speed, m.CurrentRate);
                }
                else if (m is ccminer_tpruvot)
                {
                    SetNVIDIAtpStats(AD.AlgorithmName, AD.Speed, m.CurrentRate);
                }
                else if (m is ccminer_sp)
                {
                    SetNVIDIAspStats(AD.AlgorithmName, AD.Speed, m.CurrentRate);
                }
                else if (m is sgminer)
                {
                    SetAMDOpenCLStats(AD.AlgorithmName, AD.Speed, m.CurrentRate);
                }
            }

            if (CPUAlgoName != null && CPUAlgoName.Length > 0)
            {
                SetCPUStats(CPUAlgoName, CPUTotalSpeed, CPUTotalRate);
            }
        }


        private void SetCPUStats(string aname, double speed, double paying)
        {
            label5.Text = FormatSpeedOutput(speed) + aname;
            if (aname.Equals("hodl")) label5.Text += "**";
            label_RateCPUBTC.Text = FormatPayingOutput(paying);
            label_RateCPUDollar.Text = (paying * BitcoinRate).ToString("F2", CultureInfo.InvariantCulture) + " $/" + International.GetText("form1_rate_day");
            UpdateGlobalRate();
        }


        private void SetNVIDIAtp21Stats(string aname, double speed, double paying)
        {
            label22.Text = FormatSpeedOutput(speed) + aname;
            label_RateNVIDIA2XBTC.Text = FormatPayingOutput(paying);
            label_RateNVIDIA2XDollar.Text = (paying * BitcoinRate).ToString("F2", CultureInfo.InvariantCulture) + " $/" + International.GetText("form1_rate_day");
            UpdateGlobalRate();
        }


        private void SetNVIDIAtpStats(string aname, double speed, double paying)
        {
            label8.Text = FormatSpeedOutput(speed) + aname;
            label_RateNVIDIA3XBTC.Text = FormatPayingOutput(paying);
            label_RateNVIDIA3XDollar.Text = (paying * BitcoinRate).ToString("F2", CultureInfo.InvariantCulture) + " $/" + International.GetText("form1_rate_day");
            UpdateGlobalRate();
        }


        private void SetNVIDIAspStats(string aname, double speed, double paying)
        {
            label6.Text = FormatSpeedOutput(speed) + aname;
            label_RateNVIDIA5XBTC.Text = FormatPayingOutput(paying);
            label_RateNVIDIA5XDollar.Text = (paying * BitcoinRate).ToString("F2", CultureInfo.InvariantCulture) + " $/" + International.GetText("form1_rate_day");
            UpdateGlobalRate();
        }


        private void SetAMDOpenCLStats(string aname, double speed, double paying)
        {
            label_AMDOpenCL_Mining_Speed.Text = FormatSpeedOutput(speed) + aname;
            label_RateAMDBTC.Text = FormatPayingOutput(paying);
            label_RateAMDDollar.Text = (paying * BitcoinRate).ToString("F2", CultureInfo.InvariantCulture) + " $/" + International.GetText("form1_rate_day");
            UpdateGlobalRate();
        }


        private void UpdateGlobalRate()
        {
            double TotalRate = 0;
            foreach (Miner m in Miners)
                TotalRate += m.CurrentRate;

            if (Config.ConfigData.AutoScaleBTCValues && TotalRate < 0.1)
            {
                toolStripStatusLabel_BTCDay.Text = "mBTC/" + International.GetText("form1_rate_day");
                toolStripStatusLabel4.Text = (TotalRate * 1000).ToString("F7", CultureInfo.InvariantCulture);
            }
            else
            {
                toolStripStatusLabel_BTCDay.Text = "BTC/" + International.GetText("form1_rate_day");
                toolStripStatusLabel4.Text = (TotalRate).ToString("F8", CultureInfo.InvariantCulture);
            }

            toolStripStatusLabel2.Text = (TotalRate * BitcoinRate).ToString("F2", CultureInfo.InvariantCulture);
        }


        void BalanceCheck_Tick(object sender, EventArgs e)
        {
            if (VerifyMiningAddress(false))
            {
                Helpers.ConsolePrint("NICEHASH", "Balance get");
                double Balance = NiceHashStats.GetBalance(textBox1.Text.Trim(), textBox1.Text.Trim() + "." + textBox2.Text.Trim());
                if (Balance > 0)
                {
                    if (Config.ConfigData.AutoScaleBTCValues && Balance < 0.1)
                    {
                        toolStripStatusLabel7.Text = "mBTC";
                        toolStripStatusLabel6.Text = (Balance * 1000).ToString("F7", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        toolStripStatusLabel7.Text = "BTC";
                        toolStripStatusLabel6.Text = Balance.ToString("F8", CultureInfo.InvariantCulture);
                    }

                    toolStripStatusLabel3.Text = (Balance * BitcoinRate).ToString("F2", CultureInfo.InvariantCulture);
                }
            }
        }


        void BitcoinExchangeCheck_Tick(object sender, EventArgs e)
        {
            Helpers.ConsolePrint("COINBASE", "Bitcoin rate get");
            double BR = Bitcoin.GetUSDExchangeRate();
            if (BR > 0) BitcoinRate = BR;
            Helpers.ConsolePrint("COINBASE", "Current Bitcoin rate: " + BitcoinRate.ToString("F2", CultureInfo.InvariantCulture));
        }


        void SMACheck_Tick(object sender, EventArgs e)
        {
            string worker = textBox1.Text.Trim() + "." + textBox2.Text.Trim();
            Helpers.ConsolePrint("NICEHASH", "SMA get");
            NiceHashSMA[] t = NiceHashStats.GetAlgorithmRates(worker);

            for (int i = 0; i < 3; i++)
            {
                if (t != null)
                {
                    NiceHashData = t;
                    break;
                }

                Helpers.ConsolePrint("NICEHASH", "SMA get failed .. retrying");
                System.Threading.Thread.Sleep(1000);
                t = NiceHashStats.GetAlgorithmRates(worker);
            }

            if (t == null && NiceHashData == null && ShowWarningNiceHashData)
            {
                ShowWarningNiceHashData = false;
                DialogResult dialogResult = MessageBox.Show(International.GetText("form1_msgbox_NoInternetMsg"),
                                                            International.GetText("form1_msgbox_NoInternetTitle"),
                                                            MessageBoxButtons.YesNo);

                if (dialogResult == DialogResult.Yes)
                    return;
                else if (dialogResult == DialogResult.No)
                    System.Windows.Forms.Application.Exit();
            }
        }


        void UpdateCheck_Tick(object sender, EventArgs e)
        {
            Helpers.ConsolePrint("NICEHASH", "Version get");
            string ver = NiceHashStats.GetVersion(textBox1.Text.Trim() + "." + textBox2.Text.Trim());

            if (ver == null) return;

            Version programVersion = new Version(Application.ProductVersion);
            Version onlineVersion = new Version(ver);
            int ret = programVersion.CompareTo(onlineVersion);

            if (ret < 0)
            {
                linkLabel_VisitUs.Text = String.Format(International.GetText("form1_new_version_released"), ver);
                VisitURL = "https://github.com/nicehash/NiceHashMiner/releases/tag/" + ver;
            }
        }


        void SetEnvironmentVariables()
        {
            Helpers.ConsolePrint("NICEHASH", "Setting environment variables");

            string[] envName = { "GPU_MAX_ALLOC_PERCENT", "GPU_USE_SYNC_OBJECTS",
                                 "GPU_SINGLE_ALLOC_PERCENT", "GPU_MAX_HEAP_SIZE", "GPU_FORCE_64BIT_PTR" };
            string[] envValue = { "100", "1", "100", "100", "0" };

            for (int i = 0; i < envName.Length; i++)
            {
                // Check if all the variables is set
                if (Environment.GetEnvironmentVariable(envName[i]) == null)
                {
                    try { Environment.SetEnvironmentVariable(envName[i], envValue[i]); }
                    catch (Exception e) { Helpers.ConsolePrint("NICEHASH", e.ToString()); }
                }

                // Check to make sure all the values are set correctly
                if (!Environment.GetEnvironmentVariable(envName[i]).Equals(envValue[i]))
                {
                    try { Environment.SetEnvironmentVariable(envName[i], envValue[i]); }
                    catch (Exception e) { Helpers.ConsolePrint("NICEHASH", e.ToString()); }
                }
            }
        }


        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (!VerifyMiningAddress(true)) return;

            int algo = 0;
            // find first working algo
            foreach (Miner m in Miners)
            {
                if (m.CurrentAlgo >= 0)
                {
                    algo = m.SupportedAlgorithms[m.CurrentAlgo].NiceHashID;
                    // Hack for Ethereum
                    if (algo == 19) algo = 999;
                    break;
                }
            }

            System.Diagnostics.Process.Start("http://www.nicehash.com/index.jsp?p=miners&addr=" + textBox1.Text.Trim());
        }


        private bool VerifyMiningAddress(bool ShowError)
        {
            if (!BitcoinAddress.ValidateBitcoinAddress(textBox1.Text.Trim()) && ShowError)
            {
                DialogResult result = MessageBox.Show(International.GetText("form1_msgbox_InvalidBTCAddressMsg"),
                                                      International.GetText("form1_msgbox_InvalidBTCAddressTitle"),
                                                      MessageBoxButtons.YesNo, MessageBoxIcon.Stop);
                
                if (result == System.Windows.Forms.DialogResult.Yes)
                    System.Diagnostics.Process.Start("https://www.nicehash.com/index.jsp?p=faq#faqs15");
                
                textBox1.Focus();
                return false;
            }
            else if (!BitcoinAddress.ValidateWorkerName(textBox2.Text.Trim()) && ShowError)
            {
                DialogResult result = MessageBox.Show(International.GetText("form1_msgbox_InvalidWorkerNameMsg"),
                                                      International.GetText("form1_msgbox_InvalidWorkerNameTitle"),
                                                      MessageBoxButtons.OK, MessageBoxIcon.Stop);

                textBox2.Focus();
                return false;
            }

            return true;
        }


        private void button1_Click(object sender, EventArgs e)
        {
            if (!VerifyMiningAddress(true)) return;

            if (NiceHashData == null)
            {
                MessageBox.Show(International.GetText("form1_msgbox_NullNiceHashDataMsg"),
                                International.GetText("form1_msgbox_NullNiceHashDataTitle"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Check if the user has run benchmark first
            foreach (Miner m in Miners)
            {
                if (m.EnabledDeviceCount() == 0) continue;

                if (m.CountBenchmarkedAlgos() == 0)
                {
                    DialogResult result = MessageBox.Show(String.Format(International.GetText("form1_msgbox_HaveNotBenchmarkedMsg"), m.MinerDeviceName),
                                                          International.GetText("form1_msgbox_HaveNotBenchmarkedTitle"),
                                                          MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                    if (result == System.Windows.Forms.DialogResult.Yes) break;
                    if (result == System.Windows.Forms.DialogResult.No) return;
                }
            }

            textBox1.Enabled = false;
            textBox2.Enabled = false;
            comboBox1.Enabled = false;
            buttonBenchmark.Enabled = false;
            buttonStartMining.Enabled = false;
            buttonSettings.Enabled = false;
            listView1.Enabled = false;
            buttonStopMining.Enabled = true;

            // todo: commit saving when values are changed
            Config.ConfigData.BitcoinAddress = textBox1.Text.Trim();
            Config.ConfigData.WorkerName = textBox2.Text.Trim();
            Config.ConfigData.Location = comboBox1.SelectedIndex;
            Config.Commit();

            SMAMinerCheck.Start();
            SMAMinerCheck_Tick(null, null);
            MinerStatsCheck.Start();
        }


        private void button2_Click(object sender, EventArgs e)
        {
            MinerStatsCheck.Stop();
            SMAMinerCheck.Stop();

            foreach (Miner m in Miners)
            {
                m.Stop(false);
                m.CurrentAlgo = -1;
                m.CurrentRate = 0;
            }

            SetCPUStats("", 0, 0);
            SetNVIDIAtp21Stats("", 0, 0);
            SetNVIDIAspStats("", 0, 0);
            SetNVIDIAtpStats("", 0, 0);
            SetAMDOpenCLStats("", 0, 0);

            textBox1.Enabled = true;
            textBox2.Enabled = true;
            comboBox1.Enabled = true;
            buttonBenchmark.Enabled = true;
            buttonStartMining.Enabled = true;
            buttonSettings.Enabled = true;
            listView1.Enabled = true;
            buttonStopMining.Enabled = false;

            UpdateGlobalRate();
        }


        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(VisitURL);
        }


        private void listView1_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            ComputeDevice G = e.Item.Tag as ComputeDevice;
            G.Enabled = e.Item.Checked;
            if (LoadingScreen == null)
                Config.RebuildGroups();
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach (Miner m in Miners)
                m.Stop(false);
        }


        private void button3_Click(object sender, EventArgs e)
        {
            if (!VerifyMiningAddress(true)) return;
            Config.ConfigData.Location = comboBox1.SelectedIndex;

            SMACheck.Stop();
            BenchmarkForm = new Form2(false);
            BenchmarkForm.ShowDialog();
            BenchmarkForm = null;
            SMACheck.Start();
        }


        private void button4_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(String.Format(International.GetText("form1_msgbox_buttonBenchmarkWarningMsg"), ProductName),
                                                  International.GetText("form1_msgbox_buttonBenchmarkWarningTitle"),
                                                  MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == System.Windows.Forms.DialogResult.No)
                return;

            if (!Config.ConfigData.UseNewSettingsPage)
            {
                Process PHandle = new Process();
                PHandle.StartInfo.FileName = Application.ExecutablePath;
                PHandle.StartInfo.Arguments = "-config";
                PHandle.Start();

                Close();
            }
            else
            {
                Form_Settings Settings = new Form_Settings();
                Settings.ShowDialog();

                if (Settings.ret == 1) return;

                //MessageBox.Show(String.Format(International.GetText("form1_msgbox_buttonBenchmarkRestartWarningMsg"), ProductName),
                //                String.Format(International.GetText("form1_msgbox_buttonBenchmarkRestartWarningTitle"), ProductName),
                //                MessageBoxButtons.OK, MessageBoxIcon.Information);

                Process PHandle = new Process();
                PHandle.StartInfo.FileName = Application.ExecutablePath;
                PHandle.Start();

                Close();
            }
        }


        private string FormatSpeedOutput(double speed)
        {
            string ret = "";

            if (speed < 1000)
                ret = (speed).ToString("F3", CultureInfo.InvariantCulture) + " H/s ";
            else if (speed < 100000)
                ret = (speed * 0.001).ToString("F3", CultureInfo.InvariantCulture) + " kH/s ";
            else if (speed < 100000000)
                ret = (speed * 0.000001).ToString("F3", CultureInfo.InvariantCulture) + " MH/s ";
            else
                ret = (speed * 0.000000001).ToString("F3", CultureInfo.InvariantCulture) + " GH/s ";

            return ret;
        }

        private string FormatPayingOutput(double paying)
        {
            string ret = "";

            if (Config.ConfigData.AutoScaleBTCValues && paying < 0.1)
                ret = (paying * 1000).ToString("F7", CultureInfo.InvariantCulture) + " mBTC/" + International.GetText("form1_rate_day");
            else
                ret = paying.ToString("F8", CultureInfo.InvariantCulture) + " BTC/" + International.GetText("form1_rate_day");

            return ret;
        }


        private void buttonHelp_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/nicehash/NiceHashMiner");
        }

        private void linkLabel_Choose_BTC_Wallet_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.nicehash.com/index.jsp?p=faq#faqs15");
        }

        private void toolStripStatusLabel10_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.nicehash.com/index.jsp?p=faq#faqs6");
        }

        private void toolStripStatusLabel10_MouseHover(object sender, EventArgs e)
        {
            statusStrip1.Cursor = Cursors.Hand;
        }

        private void toolStripStatusLabel10_MouseLeave(object sender, EventArgs e)
        {
            statusStrip1.Cursor = Cursors.Default;
        }

        private void textBox1_Leave(object sender, EventArgs e)
        {
            // Commit to config.json
            Config.ConfigData.BitcoinAddress = textBox1.Text.Trim();
            Config.ConfigData.WorkerName = textBox2.Text.Trim();
            Config.ConfigData.Location = comboBox1.SelectedIndex;
            Config.Commit();
        }

        private void textBox2_Leave(object sender, EventArgs e)
        {
            // Commit to config.json
            Config.ConfigData.BitcoinAddress = textBox1.Text.Trim();
            Config.ConfigData.WorkerName = textBox2.Text.Trim();
            Config.ConfigData.Location = comboBox1.SelectedIndex;
            Config.Commit();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            notifyIcon1.Icon = Properties.Resources.logo;
            notifyIcon1.Text = Application.ProductName + " v" + Application.ProductVersion + "\nDouble-click to restore..";

            if (Config.ConfigData.MinimizeToTray && FormWindowState.Minimized == this.WindowState)
            {
                notifyIcon1.Visible = true;
                this.Hide();
            }
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            notifyIcon1.Visible = false;
        }
    }
}
