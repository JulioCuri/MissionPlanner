﻿using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MissionPlanner.Controls
{
    public partial class SITL : Form
    {
        Uri sitlurl = new Uri("http://firmware.diydrones.com/Tools/MissionPlanner/sitl/");
        string sitldirectory = Application.StartupPath + Path.DirectorySeparatorChar + "sitl" + Path.DirectorySeparatorChar;

        GMapOverlay markeroverlay;

        GMapMarkerWP homemarker = new GMapMarkerWP(new PointLatLng(-34.98106, 117.85201), "H");
        bool onmarker = false;
        bool mousedown = false;
        private PointLatLng MouseDownStart;


        /*
        { "+",         MultiCopter::create },
    { "quad",      MultiCopter::create },
    { "copter",    MultiCopter::create },
    { "x",         MultiCopter::create },
    { "hexa",      MultiCopter::create },
    { "octa",      MultiCopter::create },
    { "heli",      Helicopter::create },
    { "rover",     Rover::create },
    { "crrcsim",   CRRCSim::create },
    { "jsbsim",    JSBSim::create },
    { "last_letter", last_letter::create }
             */

        ///tmp/.build/ArduCopter.elf -M+ -O-34.98106,117.85201,40,0 
        ///tmp/.build/APMrover2.elf -Mrover -O-34.98106,117.85201,40,0 
        ///tmp/.build/ArduPlane.elf -Mjsbsim -O-34.98106,117.85201,40,0 --autotest-dir ./
        ///tmp/.build/ArduCopter.elf -Mheli -O-34.98106,117.85201,40,0 


        public SITL()
        {
            InitializeComponent();

            if (!Directory.Exists(sitldirectory))
                Directory.CreateDirectory(sitldirectory);

            homemarker.Position = MainV2.comPort.MAV.cs.HomeLocation;

            myGMAP1.Position = homemarker.Position;

            myGMAP1.MapProvider = GCSViews.FlightData.mymap.MapProvider;
            myGMAP1.MaxZoom = 22;
            myGMAP1.Zoom = 16;
            myGMAP1.DisableFocusOnMouseEnter = true;

            markeroverlay = new GMapOverlay("markers");
            myGMAP1.Overlays.Add(markeroverlay);

            markeroverlay.Markers.Add(homemarker);

            myGMAP1.Invalidate();

            Utilities.ThemeManager.ApplyThemeTo(this);

            MissionPlanner.Utilities.Tracking.AddPage(this.GetType().ToString(), this.Text);
        }

        private void pictureBoxplane_Click(object sender, EventArgs e)
        {
            var exepath = CheckandGetSITLImage("ArduPlane.elf");

            StartSITL(exepath, "jsbsim", BuildHomeLocation(markeroverlay.Markers[0].Position), " --autotest-dir " + Application.StartupPath.Replace('\\','/'), 1);
        }

        private void pictureBoxrover_Click(object sender, EventArgs e)
        {
            var exepath = CheckandGetSITLImage("APMrover2.elf");

            StartSITL(exepath, "rover", BuildHomeLocation(markeroverlay.Markers[0].Position));
        }

        private void pictureBoxquad_Click(object sender, EventArgs e)
        {
            var exepath = CheckandGetSITLImage("ArduCopter.elf");

            StartSITL(exepath, "+", BuildHomeLocation(markeroverlay.Markers[0].Position));
        }

        private void pictureBoxheli_Click(object sender, EventArgs e)
        {
            var exepath = CheckandGetSITLImage("ArduHeli.elf");

            StartSITL(exepath, "heli", BuildHomeLocation(markeroverlay.Markers[0].Position));
        }

        string BuildHomeLocation(PointLatLng homelocation, int heading = 0)
        {
            return String.Format("{0},{1},{2},{3}", homelocation.Lat, homelocation.Lng, srtm.getAltitude(homelocation.Lat, homelocation.Lng).alt, heading);
        }

        private string CheckandGetSITLImage(string filename)
        {
            Uri fullurl = new Uri(sitlurl, filename);

            Common.getFilefromNet(fullurl.ToString(), sitldirectory + Path.GetFileNameWithoutExtension(filename) + ".exe");

            // dependancys
            var depurl = new Uri(sitlurl, "cyggcc_s-1.dll");
            Common.getFilefromNet(depurl.ToString(), sitldirectory + depurl.Segments[depurl.Segments.Length - 1]);
            depurl = new Uri(sitlurl, "cygstdc++-6.dll");
            Common.getFilefromNet(depurl.ToString(), sitldirectory + depurl.Segments[depurl.Segments.Length - 1]);
            depurl = new Uri(sitlurl, "cygwin1.dll");
            Common.getFilefromNet(depurl.ToString(), sitldirectory + depurl.Segments[depurl.Segments.Length - 1]);

            return sitldirectory + Path.GetFileNameWithoutExtension(filename) + ".exe";
        }

        private void StartSITL(string exepath, string model, string homelocation, string extraargs = "", int speedup = 1)
        {
            //ArduCopter.elf -M+ -O-34.98106,117.85201,40,0 
            string simdir = sitldirectory + model + Path.DirectorySeparatorChar;

            Directory.CreateDirectory(simdir);

            //File.WriteAllText(simdir + "/etc/.bashrc", "export PATH=.;$PATH");

            string path = Environment.GetEnvironmentVariable("PATH");

            Environment.SetEnvironmentVariable("PATH", Application.StartupPath + ";" + path, EnvironmentVariableTarget.Process);

            Environment.SetEnvironmentVariable("HOME", Application.StartupPath, EnvironmentVariableTarget.Process);

            ProcessStartInfo exestart = new ProcessStartInfo();
            exestart.FileName = exepath;
            exestart.Arguments = String.Format("-M{0} -O{1} -s{2} {3}", model, homelocation, speedup, extraargs);
            exestart.WorkingDirectory = simdir;
            exestart.WindowStyle = ProcessWindowStyle.Minimized;
            exestart.UseShellExecute = true;

            var proc = System.Diagnostics.Process.Start(exestart);

            System.Threading.Thread.Sleep(2000);

            var tcpserial = new Comms.TcpSerial();

            tcpserial.client = new System.Net.Sockets.TcpClient("127.0.0.1", 5760);

            MainV2.comPort.BaseStream = tcpserial;

            MainV2.comPort.Open(true, true);

            MainV2.View.ShowScreen(MainV2.View.screens[0].Name);

            this.Close();
        }

        private void myGMAP1_OnMarkerEnter(GMapMarker item)
        {
            if (!mousedown)
                onmarker = true;
        }

        private void myGMAP1_OnMarkerLeave(GMapMarker item)
        {
            if (!mousedown)
                onmarker = false;
        }

        private void myGMAP1_MouseMove(object sender, MouseEventArgs e)
        {
            if (onmarker)
            {
                if (e.Button == MouseButtons.Left)
                {
                    homemarker.Position = myGMAP1.FromLocalToLatLng(e.X, e.Y);
                }
            }
            else if (mousedown)
            {
                PointLatLng point = myGMAP1.FromLocalToLatLng(e.X, e.Y);

                double latdif = MouseDownStart.Lat - point.Lat;
                double lngdif = MouseDownStart.Lng - point.Lng;

                try
                {
                    myGMAP1.Position = new PointLatLng(myGMAP1.Position.Lat + latdif, myGMAP1.Position.Lng + lngdif);
                }
                catch { }
            }
        }

        private void myGMAP1_MouseUp(object sender, MouseEventArgs e)
        {
            mousedown = false;
            onmarker = false;
        }

        private void myGMAP1_MouseDown(object sender, MouseEventArgs e)
        {
            mousedown = true;
            MouseDownStart = myGMAP1.FromLocalToLatLng(e.X, e.Y);
        }
    }
}
