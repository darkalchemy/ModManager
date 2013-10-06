﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using ModBuilder.Forms;
using System.Net;
using System.Xml;
using Microsoft.WindowsAPICodePack;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ModBuilder
{
    public partial class Form1 : Form
    {
        // This version of Mod Builder.
        string mbversion = Properties.Settings.Default.mbVersion;
        string currmbversion = "1.4";

        #region Initialising
        string dlfilename;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (mbversion != currmbversion)
            {
                #region ALL TASKS REQUIRED TO UPDATE GO IN HERE

                #endregion

                // Update the version
                string ombversion = mbversion;
                Properties.Settings.Default.mbVersion = mbversion = currmbversion;
                Properties.Settings.Default.Save();

                MessageBox.Show("Mod Builder has successfully been updated. Enjoy!", "Mod Builder update from " + ombversion + " to " + currmbversion);
            }
            // Check for updates, if set to do so.
            versionLabel.Text = "v" + mbversion;
            if (Properties.Settings.Default.autoCheckUpdates)
                checkUpdate(false);
        }
        #endregion

        #region Opening projects
        private void openProjectButton(object sender, EventArgs e)
        {
            // Show them the loading box.
            loadProject lp = new loadProject();
            lp.Show();

            // Get us a new FolderBrowserDialog
            CommonOpenFileDialog fb = new CommonOpenFileDialog();
            fb.IsFolderPicker = true;
            fb.Title = "Please select the directory that your project resides in.";
            fb.EnsurePathExists = true;
            fb.ShowDialog();

            // Get the path.
            string dir = fb.FileName;

            // Avoid the annoying An error occured dialog.
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                lp.Close();
                return;
            }

            // Load the project.
            bool stat = lp.openProjDir(dir);

            // Check the status.
            if (stat == false)
                MessageBox.Show("An error occured while loading the project, some files could not be found or the project is corrupt. Please try repairing your project.", "Opening Mod Builder project", MessageBoxButtons.OK, MessageBoxIcon.Error);

            // Tyvm!
            lp.Close();
        }
        #endregion

        #region New project
        private void openNewProject(object sender, EventArgs e)
        {
            // Start a new instance of the Mod Editor.
            modEditor me = new modEditor();

            // Some default values.
            me.genPkgID.Checked = true;
            me.includeModManLine.Checked = true;

            me.authorName.Text = Properties.Settings.Default.idUsername;

            // Show the instance.
            me.Show();
        }
        #endregion

        #region Repair projects
        private void repairAProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CommonOpenFileDialog fb = new CommonOpenFileDialog();
            fb.IsFolderPicker = true;
            fb.Title = "Please select the directory that your project resides in.";
            fb.EnsurePathExists = true;
            fb.ShowDialog();

            // Get the path.
            string dir = fb.FileName;

            // Check if it is empty or if the user clicked Cancel.
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                return;

            // Check if it is a valid project.
            if (!Directory.Exists(dir + "/Package") || !Directory.Exists(dir + "/Source") || !File.Exists(dir + "/Package/package-info.xml"))
            {
                MessageBox.Show("The selected project is not a valid project.", "Repairing Mod Builder project", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Ask if the user wants to generate a new database, or to just add the tables.
            DialogResult result = MessageBox.Show("Should the existing database be truncated? Answering no will instead try to add all missing data.", "Reparing project", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            // New instance of the mod editor.
            modEditor me = new modEditor();

            #region Boring XML parsing
            // Try to parse the package_info.xml.
            XmlTextReader xmldoc = new XmlTextReader(dir + "/Package/package-info.xml");
            xmldoc.DtdProcessing = DtdProcessing.Ignore;
            while (xmldoc.Read())
            {
                if (xmldoc.NodeType.Equals(XmlNodeType.Element))
                {
                    switch (xmldoc.LocalName)
                    {
                        case "id":
                            string mid = xmldoc.ReadElementContentAsString();
                            me.modID.Text = mid;

                            // Determine the mod author.
                            string[] pieces = mid.Split(':');
                            me.authorName.Text = pieces[0];
                            break;

                        case "name":
                            me.modName.Text = xmldoc.ReadElementContentAsString();
                            break;

                        case "version":
                            me.modVersion.Text = xmldoc.ReadElementContentAsString();
                            break;

                        case "type":
                            if (xmldoc.ReadElementContentAsString() == "modification")
                                me.modType.SelectedItem = "Modification";
                            else
                                me.modType.SelectedItem = "Avatar pack";
                            break;

                        case "install":
                            me.modCompatibility.Text = xmldoc.GetAttribute("for");

                            break;
                    }
                }
            }
            xmldoc.Close();
            #endregion

            // Switch the result, to see what the user has answered.
            ModBuilder.Classes.ModParser mp = new ModBuilder.Classes.ModParser();
            switch (result)
            {
                // Generate an all new shiny database.
                case (DialogResult.Yes):
                    me.generateSQL(dir, true, mp.parsePackageInfo(dir + "/Package/package-info.xml"));
                    break;

                // Only add the missing tables.
                case (DialogResult.No):
                    me.generateSQL(dir, false, mp.parsePackageInfo(dir + "/Package/package-info.xml"));
                    break;

                // Or don't do anything at all! :D
                case (DialogResult.Cancel):
                    return;
            }

            // Ask if the user wants to load the project.
            result = MessageBox.Show("Your project has been repaired. Do you want to load the project now?", "Project repaired", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            // Yes? Load the project.
            if (result == DialogResult.Yes)
            {
                // Show a loadProject dialog.
                loadProject lp = new loadProject();
                lp.Show();

                // Load the project.
                bool stat = lp.openProjDir(dir);

                // Check the status.
                if (stat == false)
                    MessageBox.Show("An error occured while loading the project.", "Loading project", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Close the loadProject dialog.
                lp.Close();
            }
        }
        #endregion

        #region Convert a Package to a Project
        private void convertAPackageToAProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Forms.convertProject cp = new Forms.convertProject();
            cp.Show();
        }
        #endregion

        #region Updating
        private void ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;
        }

        private void DLUpdateCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
                return;

            DialogResult result = MessageBox.Show("The download has completed. Do you want to start the installer now? This will close Mod Manager and any open Mod Editor windows, so save your work before continuing.", "Updated Downloaded", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                System.Diagnostics.Process.Start(dlfilename);
                Close();
            }

            checkForUpdatesToolStripMenuItem.Text = "Update pending...";
            Size = new Size(Size.Width, 184);
        }

        private void checkUpdate(bool throwMessage = true)
        {
            // Catch any exceptions.
            try
            {
                // Start a new webclient.
                WebClient client = new WebClient();

                // Start a new download of the latest version number thing.
                string dver = "https://raw.github.com/Yoshi2889/ModManager/master/latestver";
                string lver = client.DownloadString(dver);

                // Pour them over as versions.
                Version mver = new Version(mbversion);
                Version lmver = new Version(lver);

                // Compare the versions.
                int status = mver.CompareTo(lmver);

                // If the status is equal to or bigger than 0 we are running the latest version.
                if (status >= 0)
                {
                    // If we are running in silent mode, skip this message.
                    if (throwMessage)
                        MessageBox.Show("You are using the latest version of Mod Builder.", "Mod Builder Updater", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                else
                {
                    DialogResult result = MessageBox.Show("A new version (" + lver + ") of Mod Manager has been released. Do you want to download and install the update?", "Mod Builder Updater", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        // Rezise the form a bit :)
                        Size = new Size(Size.Width, 247);

                        // Start downloading! DLUpdateCompleted will take over once it's done.
                        client.DownloadFileCompleted += new AsyncCompletedEventHandler(DLUpdateCompleted);
                        client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(ProgressChanged);
                        client.DownloadFileAsync(new Uri("https://github.com/Yoshi2889/ModManager/blob/master/setup.exe?raw=true"), @AppDomain.CurrentDomain.BaseDirectory + "/update_" + lver.Replace(".", "-") + ".exe");
                        dlfilename = AppDomain.CurrentDomain.BaseDirectory + "/update_" + lver.Replace(".", "-") + ".exe";
                    }
                }
            }
            catch
            {
                // Silent mode? Shhh.
                if (throwMessage)
                    MessageBox.Show("An error occured while checking for updates. Please check your internet connection or try later.", "Mod Builder Updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        private void checkForUpdatesToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(dlfilename) && File.Exists(dlfilename))
            {
                DialogResult res = MessageBox.Show("Do you want to start the updater now? This will close Mod Manager, save any open projects.", "Mod Builder Updater", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (res == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(dlfilename);
                    Close();
                }
                return;
            }

            checkUpdate();
        }
        #endregion

        #region About
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Mod Builder, a tool to help you create modifications for SMF (Simple Machines Forum).\nSMF is © Simple Machines, http://simplemachines.org/ \n Mod Builder is © Rick \"Yoshi2889\" Kerkhof");
        }
        #endregion

        #region Support
        private void supportToolstripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("http://goo.gl/WYQxf");
            }
            catch
            {
                System.Diagnostics.Process.Start("iexplore", "http://goo.gl/WYQxf");
            }
        }
        #endregion

        #region Options
        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Options op = new Options();
            op.ShowDialog();
        }
        #endregion
    }
}
