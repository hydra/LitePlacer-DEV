﻿//#define TRANSFER

/* 

 Using the standard method (Settings.settings file, Setting.xxx) was a mistake:
 No easy recovery, impossible to move installation from one machine to another, to name a few.
 This class resolves the issues. See
 http://stackoverflow.com/questions/453161/best-practice-to-save-application-settings-in-a-windows-forms-application,
 Trevor's answer (second answer from top). Also, to get a nice formatting (each parameter on its own line), 
 Newtonsoft.Json library is used. http://www.newtonsoft.com/json

 If you uncomment the first line, you get a version that transfers the settings from old version to new.

 */



using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Configuration;
using System;
using System.Xml.Linq;
using System.Drawing;

namespace LitePlacer
{
#pragma warning disable CA1031 // Do not catch general exception types (see MainForm.cs beginning)

    // =================================================================================
    // The settings
    // =================================================================================

    public class MySettings
    {

        public FormMain MainForm;

        // =================================================================================
        #region Marlin
        /*
        Marlin_xxx should be in MarlinSettings.cs in a
            public partial class MySettings
            {
            }
        block. Visual Studio bug doesn't allow that, so they are here. :-(
        */
        public double Marlin_Xspeed { get; set; } = 5000; // mm/s
        public double Marlin_XHomingSpeed { get; set; } = 500; // mm/s
        public double Marlin_XHomingBackoff { get; set; } = 5; // mm

        public double Marlin_Yspeed { get; set; } = 5000; // mm/s
        public double Marlin_YHomingSpeed { get; set; } = 500; // mm/s
        public double Marlin_YHomingBackoff { get; set; } = 5; // mm

        public double Marlin_Zspeed { get; set; } = 50; // mm/s
        public double Marlin_ZHomingSpeed { get; set; } = 500; // mm/s
        public double Marlin_ZHomingBackoff { get; set; } = 5; // mm

        public double Marlin_Aspeed { get; set; } = 200; // mm/s

        public double Marlin_Xacc { get; set; } = 1000; // mm/s^2
        public double Marlin_Yacc { get; set; } = 1000; // mm/s^2
        public double Marlin_Zacc { get; set; } = 1000; // mm/s^2
        public double Marlin_Aacc { get; set; } = 1000; // mm/s^2

        public double Marlin_XTravelPerRev { get; set; } = 40;   // mm
        public double Marlin_YTravelPerRev { get; set; } = 40;   // mm
        public double Marlin_ZTravelPerRev { get; set; } = 8;    // mm
        public double Marlin_ATravelPerRev { get; set; } = 160;  // deg

        public double Marlin_XDegPerStep { get; set; } = 0.9;   // mm
        public double Marlin_YDegPerStep { get; set; } = 0.9;   // mm
        public double Marlin_ZDegPerStep { get; set; } = 1.8;   // mm
        public double Marlin_ADegPerStep { get; set; } = 0.9;   // mm

        public int Marlin_XMicroStep { get; set; } = 16;
        public int Marlin_YMicroStep { get; set; } = 16;
        public int Marlin_ZMicroStep { get; set; } = 16;
        public int Marlin_AMicroStep { get; set; } = 16;

        public bool Marlin_XInterpolate { get; set; } = true;
        public bool Marlin_YInterpolate { get; set; } = true;
        public bool Marlin_ZInterpolate { get; set; } = true;
        public bool Marlin_AInterpolate { get; set; } = true;

        public int Marlin_XCurrent { get; set; } = 1100; //mA
        public int Marlin_YCurrent { get; set; } = 1200; //mA
        public int Marlin_ZCurrent { get; set; } = 1300; //mA
        public int Marlin_ACurrent { get; set; } = 100; //mA
        // =================================================================================
        #endregion Marlin

        public bool Cam_ShowPixels { get; set; } = false;
        public bool Cameras_KeepActive { get; set; } = false;
        public bool Cameras_RobustSwitch { get; set; } = false;

        public int CNC_AltJogSpeed { get; set; } = 4000;
        public int CNC_AspeedMax { get; set; } = 4000;
        public int CNC_CtlrJogSpeed { get; set; } = 4000;
        public bool CNC_EnableMouseWheelJog { get; set; } = true;
        public bool CNC_EnableNumPadJog { get; set; } = true;
        public int CNC_NormalJogSpeed { get; set; } = 1000;
        public string CNC_SerialPort { get; set; } = "";
        public bool CNC_SlackCompensation { get; set; } = false;
        public double SlackCompensationDistance { get; set; } = 0.4;
        public bool CNC_SlackCompensationA { get; set; } = false;
        public decimal CNC_SmallMovementSpeed { get; set; } = 150;
        public double CNC_SquareCorrection { get; set; } = 0;
        public int CNC_ZspeedMax { get; set; } = 1000;
        public double CNC_RegularMoveTimeout { get; set; } = 10.0;
        public bool CNC_OptimizeA { get; set; } = true;
        public FormMain.ControlBoardType Controlboard { get; set; } = FormMain.ControlBoardType.unknown;
        // must match CNC.cs definition of ControlBoardType
        private string EndCharacters = "\n";
        public string Serial_EndCharacters
        {
            get
            {
                return EndCharacters;
            }
            set
            {
                EndCharacters = value;
                setEOLchars(value);
            }
        }
        public double General_BelowPCB_Allowance { get; set; } = 3;
        public bool General_CheckForUpdates { get; set; } = false;
        public double General_JigOffsetX { get; set; } = 10;
        public double General_JigOffsetY { get; set; } = 10;
        public double General_MachineSizeX { get; set; } = 590;
        public double General_MachineSizeY { get; set; } = 370;
        public double General_NegativeX { get; set; } = 3;
        public double General_NegativeY { get; set; } = 3;
        public string General_Mark1Name { get; set; } = "Mark1";
        public double General_Mark1X { get; set; } = 0;
        public double General_Mark1Y { get; set; } = 0;
        public double General_Mark1A { get; set; } = 0;
        public string General_Mark2Name { get; set; } = "Mark2";
        public double General_Mark2X { get; set; } = 0;
        public double General_Mark2Y { get; set; } = 0;
        public double General_Mark2A { get; set; } = 0;
        public string General_Mark3Name { get; set; } = "Mark3";
        public double General_Mark3X { get; set; } = 0;
        public double General_Mark3Y { get; set; } = 0;
        public double General_Mark3A { get; set; } = 0;
        public string General_Mark4Name { get; set; } = "Mark4";
        public double General_Mark4X { get; set; } = 0;
        public double General_Mark4Y { get; set; } = 0;
        public double General_Mark4A { get; set; } = 0;
        public string General_Mark5Name { get; set; } = "Mark5";
        public double General_Mark5X { get; set; } = 0;
        public double General_Mark5Y { get; set; } = 0;
        public double General_Mark5A { get; set; } = 0;
        public string General_Mark6Name { get; set; } = "Mark6";
        public double General_Mark6X { get; set; } = 0;
        public double General_Mark6Y { get; set; } = 0;
        public double General_Mark6A { get; set; } = 0;
        public string General_MediumSpeed { get; set; } = "50000";
        public bool General_MuteLogging { get; set; } = false;
        public double General_ParkX { get; set; } = 0;
        public double General_ParkY { get; set; } = 0;
        public double General_PickupCenterX { get; set; } = 0;
        public double General_PickupCenterY { get; set; } = 0;
        public int General_PickupReleaseTime { get; set; } = 150;
        public int General_PickupVacuumTime { get; set; } = 250;
        public double General_FullDownToJustTouchingDifference { get; set; } = 0.2;
        public double General_ShadeGuard_mm { get; set; } = 0;
        public bool General_UpgradeRequired { get; set; } = true;
        public double Job_Xoffset { get; set; } = 0;
        public double Job_Yoffset { get; set; } = 0;
        public bool General_VigorousHoming { get; set; } = false;
        public bool General_PumpOutputInverted { get; set; } = false;
        public bool General_VacuumOutputInverted { get; set; } = false;
        public bool General_SaveFilesAtClosing { get; set; } = true;
        public bool General_Autopark { get; set; } = false;
        public double General_ZTestTravel { get; set; } = 20;

        public double General_Z0toPCB { get; set; } = 0;
        public bool General_ZlbFixAsked { get; set; } = false;
        public int NozzleHeightSetupStage { get; set; } = 0;

        public double General_ZTouchDifference { get; set; } = 2.0;
        public bool General_HeightCalibrationDone { get; set; } = false;

        public bool Nozzles_AfullSpeed { get; set; } = true;
        public double Nozzles_Aspeed { get; set; } = 500;
        public double Nozzles_CalibrationDistance { get; set; } = 4;
        public double Nozzles_CalibrationMaxSize { get; set; } = 2;
        public double Nozzles_CalibrationMinSize { get; set; } = 0;
        public int Nozzles_count { get; set; } = 6;
        public int Nozzles_current { get; set; } = 1;
        public int Nozzles_default { get; set; } = 1;
        public bool Nozzles_Enabled { get; set; } = false;
        public bool Nozzles_FirstMoveFullSpeed { get; set; } = true;
        public bool Nozzles_FirstMoveSlackCompensation { get; set; } = true;
        public bool Nozzles_LastMoveFullSpeed { get; set; } = false;
        public int Nozzles_maximum { get; set; } = 10;
        public int Nozzles_Timeout { get; set; } = 5000;
        public bool Nozzles_XYfullSpeed { get; set; } = false;
        public double Nozzles_XYspeed { get; set; } = 500;
        public bool Nozzles_ZfullSpeed { get; set; } = false;
        public double Nozzles_Zspeed { get; set; } = 500;
        public double Nozzles_WarningTreshold { get; set; } = 1.0;

        public bool Panel_UseBoardFids { get; set; } = true;
        public double Panel_XFirstOffset { get; set; } = 0;
        public double Panel_XIncrement { get; set; } = 0;
        public int Panel_XRepeats { get; set; } = 0;
        public double Panel_YFirstOffset { get; set; } = 0;
        public double Panel_YIncrement { get; set; } = 0;
        public int Panel_YRepeats { get; set; } = 0;

        public double Placement_Pickup_Depth { get; set; } = 0.2;
        public double Placement_Placement_Depth { get; set; } = 1;
        public bool Placement_FiducialConfirmation { get; set; } = false;
        public int Placement_FiducialsType { get; set; } = 0;
        public bool Placement_OmitNozzleCalibration { get; set; } = false;
        public bool Placement_SkipMeasurements { get; set; } = false;
        public bool Placement_UpdateJobGridAtRuntime { get; set; } = false;

        public bool DownCam_DrawBox { get; set; } = true;
        public bool DownCam_DrawCross { get; set; } = true;
        public bool DownCam_DrawSidemarks { get; set; } = true;
        public bool DownCam_DrawTicks { get; set; } = true;
        // public int DownCam_index { get; set; } = -1;
        public bool Downcam_InvertedHomeMark { get; set; } = false;
        public int DownCam_MeasurementDelay { get; set; } = 6;
        public double DownCam_NozzleOffsetX { get; set; } = 84;
        public double DownCam_NozzleOffsetY { get; set; } = 27;
        public System.Drawing.Color DownCam_SnapshotColor { get; set; } = System.Drawing.Color.White;
        public double DownCam_XmmPerPixel { get; set; } = 0.1;
        public double DownCam_YmmPerPixel { get; set; } = 0.1;
        public bool DownCam_Zoom { get; set; } = false;
        public double DownCam_Zoomfactor { get; set; } = 1.5;
        public string DowncamMoniker { get; set; } = "";
        public string Downcam_Name { get; set; } = "";
        public bool Downcam_Mirror { get; set; } = false;

        public int DownCam_DesiredX { get; set; } = 1280;
        public int DownCam_DesiredY { get; set; } = 1024;
        public bool DownCam_UseMaxResolution = true;

        public bool UpCam_DrawBox { get; set; } = true;
        public bool UpCam_DrawCross { get; set; } = true;
        public bool UpCam_DrawSidemarks { get; set; } = true;
        // public int UpCam_index { get; set; } = -1;
        public int UpCam_MeasurementDelay { get; set; } = 6;
        public double UpCam_PositionX { get; set; } = 2;
        public double UpCam_PositionY { get; set; } = 3;
        public System.Drawing.Color UpCam_SnapshotColor { get; set; } = System.Drawing.Color.White;
        public double UpCam_XmmPerPixel { get; set; } = 0.05;
        public double UpCam_YmmPerPixel { get; set; } = 0.05;
        public bool UpCam_Zoom { get; set; } = false;
        public double UpCam_Zoomfactor { get; set; } = 1.5;
        public string UpcamMoniker { get; set; } = "";
        public string Upcam_Name { get; set; } = "";
        public bool Upcam_Mirror { get; set; } = true;
        public int UpCam_DesiredX { get; set; } = 1280;
        public int UpCam_DesiredY { get; set; } = 1024;
        public bool UpCam_UseMaxResolution = true;

        // =================================================================================
        // 
        // =================================================================================

        public void setEOLchars(string val)
        {
            if (MainForm == null) return;       // startup
            MainForm.EOL_textBox.Text = "test";
        }

        public bool Save(MySettings pSettings, string FileName)
        {
            try
            {
                FormMain Fsave;

                MainForm.DisplayText("Saving application settings to " + FileName);
                Fsave = pSettings.MainForm;
                pSettings.MainForm = null;  // Otherwise self-referecing loop
                File.WriteAllText(FileName, JsonConvert.SerializeObject(pSettings, Formatting.Indented));
                pSettings.MainForm = Fsave;
                MainForm.DisplayText("Done.");
                return true;
            }
            catch (Exception excep)
            {
                MainForm.DisplayText("Application settings save failed: " + excep.Message, KnownColor.DarkRed, true);
                return false;
            }
        }


        public MySettings Load(string FileName)
        {
            try
            {

#if TRANSFER
                TransferSettings(FileName);  // does not return
                // in case it does:
                MainForm.ShowMessageBox(
                   "TransferSettings(FileName) returned!\n" +
                   "Please report this issue to juha@kuusama.com.",
                   "Sloppy progrfammer error",
                   MessageBoxButtons.OK);
                Application.Exit();

#endif

                MySettings settings = new MySettings();
                if (File.Exists(FileName))
                {
                    MainForm.DisplayText("Reading application settings from " + FileName);
                    settings = JsonConvert.DeserializeObject<MySettings>(File.ReadAllText(FileName));

                    // JsonConvert.DeserializeObject can return null if the setings file is corrupt,
                    // catch this here and inform the operator before we cause a NullReferenceException in Form1_Load.
                    if (settings == null)
                    {
                        throw new Exception($"Couldn't load {FileName}. File exists but is corrupt.");
                    }
                    settings.MainForm = MainForm;
                    return settings;
                }
                else
                {
                    DialogResult dialogResult = MainForm.ShowMessageBox(
                       "SAVED SETTINGS FILE NOT FOUND. \n\n" +
                       "If this is the first time you are running this program, this is expected. " +
                       "Click OK, startup continues and the program uses built-in default values.\n\n" +
                       "If you are updating from an earlier version, click cancel now, " +
                       "then run the settings transfer program." +
                       "Please see https://www.liteplacer.com/downloads/ for instructions and download link.",
                       "Settings file not found", MessageBoxButtons.OKCancel);
                    if (dialogResult == DialogResult.Cancel)
                    {
                        Environment.Exit(0);
                    }
                    settings.MainForm = MainForm;
                    return settings;
                }
            }
            catch (Exception excep)
            {
                MySettings s = new MySettings();
                s.MainForm = MainForm;


                DialogResult dialogResult = MainForm.ShowMessageBox(
                    "Problem loading application settings:\n" + excep.Message +
                    " \n\rExit program without saving any data? \n\r" +
                   "If \"Yes\", you lose changes since last start.\n\r" +
                   "If \"No\", continue with default settings. ** Your old settings will be\n\r" +
                   "overwritten at program end. **",
                   "Data save problem", MessageBoxButtons.YesNo);
                if (dialogResult == DialogResult.No)
                {
                    return s;
                }
                s.General_SaveFilesAtClosing = false;
                Application.Exit();
                return s;    // to avoid compile error
            }
        }

#if TRANSFER

        private void CopySettings(string FileName)
        {
            try
            {
                Properties.Settings.Default.Upgrade();
                MySettings s = new MySettings();
                s.Cameras_KeepActive = Properties.Settings.Default.Cameras_KeepActive;
                s.Cameras_RobustSwitch = Properties.Settings.Default.Cameras_RobustSwitch;
                s.CNC_AltJogSpeed = Properties.Settings.Default.CNC_AltJogSpeed;
                s.CNC_AspeedMax = Properties.Settings.Default.CNC_AspeedMax;
                s.CNC_CtlrJogSpeed = Properties.Settings.Default.CNC_CtlrJogSpeed;
                s.CNC_EnableMouseWheelJog = Properties.Settings.Default.CNC_EnableMouseWheelJog;
                s.CNC_EnableNumPadJog = Properties.Settings.Default.CNC_EnableNumPadJog;
                s.CNC_NormalJogSpeed = Properties.Settings.Default.CNC_NormalJogSpeed;
                s.CNC_SerialPort = Properties.Settings.Default.CNC_SerialPort;
                s.CNC_SlackCompensation = Properties.Settings.Default.CNC_SlackCompensation;
                s.CNC_SlackCompensationA = Properties.Settings.Default.CNC_SlackCompensationA;
                s.CNC_SmallMovementSpeed = Properties.Settings.Default.CNC_SmallMovementSpeed;
                s.CNC_SquareCorrection = Properties.Settings.Default.CNC_SquareCorrection;
                s.CNC_ZspeedMax = Properties.Settings.Default.CNC_ZspeedMax;
                s.DownCam_DrawTicks = Properties.Settings.Default.DownCam_DrawTicks;
                s.DownCam_index = Properties.Settings.Default.DownCam_index;
                s.Downcam_InvertedHomeMark = Properties.Settings.Default.Downcam_InvertedHomeMark;
                s.DownCam_MeasurementDelay = Properties.Settings.Default.DownCam_MeasurementDelay;
                s.DownCam_NozzleOffsetX = Properties.Settings.Default.DownCam_NozzleOffsetX;
                s.DownCam_NozzleOffsetY = Properties.Settings.Default.DownCam_NozzleOffsetY;
                s.DownCam_SnapshotColor = Properties.Settings.Default.DownCam_SnapshotColor;
                s.DownCam_XmmPerPixel = Properties.Settings.Default.DownCam_XmmPerPixel;
                s.DownCam_YmmPerPixel = Properties.Settings.Default.DownCam_YmmPerPixel;
                s.DownCam_Zoom = Properties.Settings.Default.DownCam_Zoom;
                s.DownCam_Zoomfactor = Properties.Settings.Default.DownCam_Zoomfactor;
                s.DowncamMoniker = Properties.Settings.Default.DowncamMoniker;
                s.General_BelowPCB_Allowance = Properties.Settings.Default.General_BelowPCB_Allowance;
                s.General_CheckForUpdates = Properties.Settings.Default.General_CheckForUpdates;
                s.General_JigOffsetX = Properties.Settings.Default.General_JigOffsetX;
                s.General_JigOffsetY = Properties.Settings.Default.General_JigOffsetY;
                s.General_MachineSizeX = Properties.Settings.Default.General_MachineSizeX;
                s.General_MachineSizeY = Properties.Settings.Default.General_MachineSizeY;
                s.General_Mark1Name = Properties.Settings.Default.General_Mark1Name;
                s.General_Mark1X = Properties.Settings.Default.General_Mark1X;
                s.General_Mark1Y = Properties.Settings.Default.General_Mark1Y;
                s.General_Mark2Name = Properties.Settings.Default.General_Mark2Name;
                s.General_Mark2X = Properties.Settings.Default.General_Mark2X;
                s.General_Mark2Y = Properties.Settings.Default.General_Mark2Y;
                s.General_Mark3Name = Properties.Settings.Default.General_Mark3Name;
                s.General_Mark3X = Properties.Settings.Default.General_Mark3X;
                s.General_Mark3Y = Properties.Settings.Default.General_Mark3Y;
                s.General_Mark4Name = Properties.Settings.Default.General_Mark4Name;
                s.General_Mark4X = Properties.Settings.Default.General_Mark4X;
                s.General_Mark4Y = Properties.Settings.Default.General_Mark4Y;
                s.General_Mark5Name = Properties.Settings.Default.General_Mark5Name;
                s.General_Mark5X = Properties.Settings.Default.General_Mark5X;
                s.General_Mark5Y = Properties.Settings.Default.General_Mark5Y;
                s.General_Mark6Name = Properties.Settings.Default.General_Mark6Name;
                s.General_Mark6X = Properties.Settings.Default.General_Mark6X;
                s.General_Mark6Y = Properties.Settings.Default.General_Mark6Y;
                s.General_MediumSpeed = Properties.Settings.Default.General_MediumSpeed;
                s.General_MuteLogging = Properties.Settings.Default.General_MuteLogging;
                s.General_ParkX = Properties.Settings.Default.General_ParkX;
                s.General_ParkY = Properties.Settings.Default.General_ParkY;
                s.General_PickupCenterX = Properties.Settings.Default.General_PickupCenterX;
                s.General_PickupCenterY = Properties.Settings.Default.General_PickupCenterY;
                s.General_PickupReleaseTime = Properties.Settings.Default.General_PickupReleaseTime;
                s.General_PickupVacuumTime = Properties.Settings.Default.General_PickupVacuumTime;
                s.General_PlacementBackOff = Properties.Settings.Default.General_PlacementBackOff;
                s.General_ShadeGuard_mm = Properties.Settings.Default.General_ShadeGuard_mm;
                s.General_UpgradeRequired = Properties.Settings.Default.General_UpgradeRequired;
                s.General_ZprobingHysteresis = Properties.Settings.Default.General_ZprobingHysteresis;
                s.General_ZTestTravel = Properties.Settings.Default.General_ZTestTravel;
                s.General_ZtoPCB = Properties.Settings.Default.General_ZtoPCB;
                s.Job_Xoffset = Properties.Settings.Default.Job_Xoffset;
                s.Job_Yoffset = Properties.Settings.Default.Job_Yoffset;
                s.Nozzles_AfullSpeed = Properties.Settings.Default.Nozzles_AfullSpeed;
                s.Nozzles_Aspeed = Properties.Settings.Default.Nozzles_Aspeed;
                s.Nozzles_CalibrationDistance = Properties.Settings.Default.Nozzles_CalibrationDistance;
                s.Nozzles_CalibrationMaxSize = Properties.Settings.Default.Nozzles_CalibrationMaxSize;
                s.Nozzles_CalibrationMinSize = Properties.Settings.Default.Nozzles_CalibrationMinSize;
                s.Nozzles_count = Properties.Settings.Default.Nozzles_count;
                s.Nozzles_current = Properties.Settings.Default.Nozzles_current;
                s.Nozzles_default = Properties.Settings.Default.Nozzles_default;
                s.Nozzles_Enabled = Properties.Settings.Default.Nozzles_Enabled;
                s.Nozzles_FirstMoveFullSpeed = Properties.Settings.Default.Nozzles_FirstMoveFullSpeed;
                s.Nozzles_FirstMoveSlackCompensation = Properties.Settings.Default.Nozzles_FirstMoveSlackCompensation;
                s.Nozzles_LastMoveFullSpeed = Properties.Settings.Default.Nozzles_LastMoveFullSpeed;
                s.Nozzles_maximum = Properties.Settings.Default.Nozzles_maximum;
                s.Nozzles_Timeout = Properties.Settings.Default.Nozzles_Timeout;
                s.Nozzles_XYfullSpeed = Properties.Settings.Default.Nozzles_XYfullSpeed;
                s.Nozzles_XYspeed = Properties.Settings.Default.Nozzles_XYspeed;
                s.Nozzles_ZfullSpeed = Properties.Settings.Default.Nozzles_ZfullSpeed;
                s.Nozzles_Zspeed = Properties.Settings.Default.Nozzles_Zspeed;
                s.Panel_UseBoardFids = Properties.Settings.Default.Panel_UseBoardFids;
                s.Panel_XFirstOffset = Properties.Settings.Default.Panel_XFirstOffset;
                s.Panel_XIncrement = Properties.Settings.Default.Panel_XIncrement;
                s.Panel_XRepeats = Properties.Settings.Default.Panel_XRepeats;
                s.Panel_YFirstOffset = Properties.Settings.Default.Panel_YFirstOffset;
                s.Panel_YIncrement = Properties.Settings.Default.Panel_YIncrement;
                s.Panel_YRepeats = Properties.Settings.Default.Panel_YRepeats;
                s.Placement_Depth = Properties.Settings.Default.Placement_Depth;
                s.Placement_FiducialConfirmation = Properties.Settings.Default.Placement_FiducialConfirmation;
                s.Placement_FiducialsType = Properties.Settings.Default.Placement_FiducialsType;
                s.Placement_FiducialTolerance = Properties.Settings.Default.Placement_FiducialTolerance;
                s.Placement_OmitNozzleCalibration = Properties.Settings.Default.Placement_OmitNozzleCalibration;
                s.Placement_SkipMeasurements = Properties.Settings.Default.Placement_SkipMeasurements;
                s.Placement_UpdateJobGridAtRuntime = Properties.Settings.Default.Placement_UpdateJobGridAtRuntime;
                s.UpCam_index = Properties.Settings.Default.UpCam_index;
                s.UpCam_MeasurementDelay = Properties.Settings.Default.UpCam_MeasurementDelay;
                s.UpCam_PositionX = Properties.Settings.Default.UpCam_PositionX;
                s.UpCam_PositionY = Properties.Settings.Default.UpCam_PositionY;
                s.UpCam_SnapshotColor = Properties.Settings.Default.UpCam_SnapshotColor;
                s.UpCam_XmmPerPixel = Properties.Settings.Default.UpCam_XmmPerPixel;
                s.UpCam_YmmPerPixel = Properties.Settings.Default.UpCam_YmmPerPixel;
                s.UpCam_Zoom = Properties.Settings.Default.UpCam_Zoom;
                s.UpCam_Zoomfactor = Properties.Settings.Default.UpCam_Zoomfactor;
                s.UpcamMoniker = Properties.Settings.Default.UpcamMoniker;

                if (Save(s, FileName))
                {
                    MainForm.ShowMessageBox(
                        "New format settings file created.\n" +
                        "You can now update the software.",
                        "Settings saved",
                        MessageBoxButtons.OK);
                }
            }
            catch (System.Exception excep)
            {
                MainForm.ShowMessageBox(
                    "Problem saving the new format file:\n" + excep.Message + "\n",
                    "Problem in save",
                    MessageBoxButtons.OK);
            }
            Environment.Exit(0);
        }

        private void TransferSettings(string FileName)
        {
            // This routine gets settings from old version and creates the file

            DialogResult dialogResult = MessageBox.Show(
                "This version of the LitePlacer software only transfers application settings from old format to new.\n" +
                "For more information, please see link in /downloads page." + "Continue?",
                "Copy settings from old version to new version",
                MessageBoxButtons.YesNo);

            if (dialogResult == DialogResult.No)
            {
                Environment.Exit(0);
                // Application.Exit();
            }

            if (File.Exists(FileName))
            {
                MainForm.ShowMessageBox(
                   "New format application settings file found, nothing done.\n" +
                   "You can now update the software.",
                   "Settings already transferred",
                   MessageBoxButtons.OK);
                Environment.Exit(0);
            }
            else
            {
                CopySettings(FileName);
            }
        }
#endif
    }
}
