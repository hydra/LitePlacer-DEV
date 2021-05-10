﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Globalization;

using System.Drawing;
using System.Threading;
using System.Drawing.Imaging;
using System.Diagnostics;

using AForge;
using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Imaging;
using AForge.Imaging.Filters;
using AForge.Math.Geometry;




namespace LitePlacer
{
    public class Camera
    {

        // Program has been crashing due to access of ImageBox.  As a shared resource it needs
        // to be accessed in protected regions.  The lock _locker is to be used for this purpose.
        // The OnPaint method in PictureBox runs in a second task.  By extending the PictureBox
        // class, overriding the OnPaint method and putting the call into a region protected
        // by _locker we can stop the crashing.

#pragma warning disable CA1034 // Nested types should not be visible
        public class ProtectedPictureBox : System.Windows.Forms.PictureBox
#pragma warning restore CA1034 // Nested types should not be visible
        {
            protected override void OnPaint(PaintEventArgs e)
            {
                lock (_locker)
                {
                    base.OnPaint(e);
                }
            }

            // _locker must be a static variable to be available to the overridden OnPaint method
            public static object _locker { get; set; } = new object();
        }

        private VideoCaptureDevice VideoSource = null;
        private FormMain MainForm;

        public Camera(FormMain MainF)
        {
            MainForm = MainF;
        }

        public bool Active { get; set; }

        public bool IsRunning()
        {
            if (VideoSource != null)
            {
                return (VideoSource.IsRunning);
            };
            return false;
        }

        public void SignalToStop()      // Asks nicely
        {
            VideoSource.SignalToStop();
        }

        public void NakedStop()         // Tries to force it (but still doesn't always work, just like with children)
        {
            VideoSource.Stop();
        }

        public void Close()
        {
            if (!(VideoSource == null))
            {
                if (!VideoSource.IsRunning)
                {
                    return;
                }
                MainForm.DisplayText("Stopping " + Id + ": " + MonikerString);
                VideoSource.SignalToStop();
                VideoSource.WaitForStop();  // problem? 
                int i = 0;
                while (VideoSource.IsRunning && i<10)
                {
                    VideoSource.Stop();
                    Thread.Sleep(20);
                    Application.DoEvents();
                }
                if (i>=10)
                {
                    MainForm.DisplayText("*** "+ Id + " refuses to stop! ***" + MonikerString, KnownColor.DarkRed, true);
                }
                VideoSource.NewFrame -= new NewFrameEventHandler(Video_NewFrame);
                VideoSource = null;
                MonikerString = "Stopped";
            }
        }

        public void DisplayPropertyPage()
        {
            VideoSource.DisplayPropertyPage(IntPtr.Zero);

        }

        // Image= PictureBox in UI, the final shown image
        // Frame= picture from camera

        // All processing and returned values are in Frame content
        // All references to PictureBox must be replace with ProtectedPictureBox
        ProtectedPictureBox _imageBox;
        public ProtectedPictureBox ImageBox
        {
            get
            {
                return (_imageBox);
            }
            set
            {
                lock (ProtectedPictureBox._locker)
                {
                    _imageBox = value;
                }
            }
        }

        public int FrameCenterX { get; set; }
        public int FrameCenterY { get; set; }
        public int FrameSizeX { get; set; }
        public int FrameSizeY { get; set; }
        public int DesiredX { get; set; }       // requested resolution
        public int DesiredY { get; set; }
        public System.Drawing.Point Resolution { get; set; }  // resolution in use

        public string MonikerString { get; set; } = "unconnected";
        public string Id { get; set; } = "unconnected";

        public bool ReceivingFrames { get; set; }

        public bool Mirror { get; set; }    // If image is mirrored (On upcam, thins is more logical)


        // =================================================================================================

        private System.Drawing.Point FindMaxResolution(List<System.Drawing.Point> Resolutions)
        {
            System.Drawing.Point res = new System.Drawing.Point(0, 0);
            if (Resolutions==null)
            {
                return res;
            }
            List<System.Drawing.Point> InOrder= Resolutions.OrderByDescending(o => o.X).ToList();
            return InOrder[0];
        }

        // =================================================================================================
        public List<System.Drawing.Point> GetResolutions(string MonikerStr)
        {
            FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            List<System.Drawing.Point> Resolutions = new List<System.Drawing.Point>();
            if (videoDevices == null)
            {
                MainForm.DisplayText("No Cameras.", KnownColor.Purple);
                return Resolutions;
            }
            if (videoDevices.Count == 0)
            {
                MainForm.DisplayText("No Cameras.", KnownColor.Purple);
                return Resolutions;
            }
            try
            {
                VideoCaptureDevice source = new VideoCaptureDevice(MonikerStr);
                int tries = 0;
                System.Drawing.Point reso = new System.Drawing.Point();
                while (tries < 4)
                {
                    if (source == null)
                    {
                        Thread.Sleep(20);
                        tries++;
                        break;
                    }
                    if (source.VideoCapabilities.Length > 0)
                    {
                        for (int i = 0; i < source.VideoCapabilities.Length; i++)
                        {
                            reso.X = source.VideoCapabilities[i].FrameSize.Width;
                            reso.Y = source.VideoCapabilities[i].FrameSize.Height;
                            MainForm.DisplayText("X: " + reso.X.ToString(CultureInfo.InvariantCulture) +
                                ", Y: " + reso.Y.ToString(CultureInfo.InvariantCulture));
                            Resolutions.Add(reso);
                        }
                        return Resolutions;
                    }
                }
                // if we didn't return from above:
                MainForm.DisplayText("Could not get resolution info from camera.", KnownColor.Purple);
                return Resolutions;
            }
            catch (Exception)
            {
                MainForm.DisplayText("Could not get resolution info from camera.", KnownColor.Purple);
                return Resolutions;
            }

        }

        // =================================================================================================
        public bool Start(string cam, string MonikerStr, bool UseMaxRes)
        {
            try
            {
                MainForm.DisplayText(cam + " start, moniker= " + MonikerStr);

                // enumerate video devices
                FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                // create the video source (check that the camera exists is already done
                VideoSource = new VideoCaptureDevice(MonikerStr);
                int tries = 0;

                if (UseMaxRes)
                {
                    bool fine = false;
                    List<System.Drawing.Point> Resolutions = GetResolutions(MonikerStr);
                    if (Resolutions.Count==0)
                    {
                        MainForm.DisplayText("Could not set resolution");
                        return false;
                    }
                    System.Drawing.Point MaxRes = FindMaxResolution(Resolutions);
                    for (int i = 0; i < VideoSource.VideoCapabilities.Length; i++)
                    {
                        if ((VideoSource.VideoCapabilities[i].FrameSize.Width == MaxRes.X)
                            &&
                            (VideoSource.VideoCapabilities[i].FrameSize.Height == MaxRes.Y))
                        {
                            VideoSource.VideoResolution = VideoSource.VideoCapabilities[i];
                            fine = true;
                            break;
                        }
                    }
                    if (!fine)
                    {
                        MainForm.DisplayText("Could not set resolution");
                        return false;
                    }
                    Resolution = MaxRes;
                }
                else
                {
                    tries = 0;
                    while (tries < 4)
                    {
                        if (VideoSource != null)
                        {
                            break;
                        }
                        else
                        {
                            Thread.Sleep(10);
                            tries++;
                        }
                    }
                    if (tries >= 4)
                    {
                        MainForm.DisplayText("Could not get resolution info");
                        return false;
                    }
                    if (VideoSource.VideoCapabilities.Length <= 0)
                    {
                        MainForm.DisplayText("Could not get resolution info");
                        return false;
                    }
                    bool fine = false;
                    for (int i = 0; i < VideoSource.VideoCapabilities.Length; i++)
                    {
                        if ((VideoSource.VideoCapabilities[i].FrameSize.Width == DesiredX)
                            &&
                            (VideoSource.VideoCapabilities[i].FrameSize.Height == DesiredY))
                        {
                            VideoSource.VideoResolution = VideoSource.VideoCapabilities[i];
                            fine = true;
                            break;
                        }
                    }
                    if (!fine)
                    {
                        MainForm.DisplayText("Desired resolution not available");
                        return false;
                    }
                    System.Drawing.Point res = new System.Drawing.Point(DesiredX, DesiredY);
                    Resolution = res;
                }

                VideoSource.NewFrame += new NewFrameEventHandler(Video_NewFrame);
                ReceivingFrames = false;

                // try ten times to start
                tries = 0;
                while (tries < 80)  // 4s maximum to a camera to start
                {
                    // VideoSource.Start() checks running status, is safe to call multiple times
                    tries++;
                    if (VideoSource==null)
                    {
                        break;      // this can happen if changing tabs too fast. TODO: True guard for operations during camera switch/start
                    }
                    VideoSource.Start();
                    if (!ReceivingFrames)
                    {
                        // 50 ms pause, processing events so that videosource has a chance
                        for (int i = 0; i < 5; i++)
                        {
                            Thread.Sleep(10);
                            Application.DoEvents();
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                if (!ReceivingFrames)
                {
                    MainForm.DisplayText("Camera started, but is not sending video");
                    return false;
                }
                MainForm.DisplayText("*** Camera started: " + tries.ToString(CultureInfo.InvariantCulture), KnownColor.Purple);

                // We managed to start the camera using desired resolution
                FrameSizeX = Resolution.X;
                FrameSizeY = Resolution.Y;
                FrameCenterX = FrameSizeX / 2;
                FrameCenterY = FrameSizeY / 2;
                PauseProcessing = false;
                return true;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (System.Exception excep)
            {
                MessageBox.Show(excep.Message);
                return false;
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        public List<string> GetDeviceList()
        {
            List<string> Devices = new List<string>();

            FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo device in videoDevices)
            {
                Devices.Add(device.Name);
            }
            return (Devices);
        }

        public List<string> GetMonikerStrings()
        {
            List<string> Monikers = new List<string>();

            FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo device in videoDevices)
            {
                Monikers.Add(device.MonikerString);
            }
            return (Monikers);
        }



        // ==========================================================================================================
        // Video processing and measurements are done by appying AForge functions one by one to a videoframe.
        // To do this, lists of functions are maintained.
        // ==========================================================================================================

        // The list of functions processing the image shown to user:
        List<AForgeFunction> DisplayFunctions = new List<AForgeFunction>();

        enum DataGridViewColumns { Function, Active, Int, Double, R, G, B };

        public delegate void AForge_op(ref Bitmap frame, int par_int, double par_d, int par_R, int par_G, int par_B, 
            double par_dA, double par_dB, double par_dC);
        class AForgeFunction
        {
            public AForge_op func { get; set; }
            public int parameter_int { get; set; }              // general parameters. Some functions take one int,
            public double parameter_double { get; set; }        // some take a float,
            public int R { get; set; }              // and some need R, B, G values.
            public int G { get; set; }
            public int B { get; set; }
            public double parameter_doubleA { get; set; }
            public double parameter_doubleB { get; set; }
            public double parameter_doubleC { get; set; }
        }


        // ==========================================================================================================
        // Convert from UI functions (AForgeFunctionDefinition) to processing functions (AForgeFunction)

        private List<AForgeFunction> BuildFunctionsList(List<AForgeFunctionDefinition> UiList)
        {
            List<AForgeFunction> NewList = new List<AForgeFunction>();
            NewList.Clear();
            MainForm.DisplayText("BuildFunctionsList: ");
            foreach (AForgeFunctionDefinition UIfucnt in UiList)
            {
                AForgeFunction f = new AForgeFunction();
                // skip inactive rows
                if (UIfucnt == null)
                {
                    continue;
                }
                if (!UIfucnt.Active)
                {
                    continue;
                }
                if (UIfucnt.Name == "Jog before measurement")
                {
                    JoggingRequested = true;
                    continue;
                }
                switch (UIfucnt.Name)
                {
                    case "Grayscale":
                        f.func = GrayscaleFunc;
                        break;

                    case "Contrast scretch":
                        f.func = Contrast_scretchFunc;
                        break;

                    case "Kill color":
                        f.func = KillColor_Func;
                        break;

                    case "Keep color":
                        f.func = KeepColor_Func;
                        break;

                    case "Invert":
                        f.func = InvertFunct;
                        break;

                    case "Erosion":
                        f.func = ErosionFunct;
                        break;

                    case "Meas. zoom":
                        f.func = Meas_ZoomFunc;
                        break;

                    case "Edge detect":
                        f.func = Edge_detectFunc;
                        break;

                    case "Noise reduction":
                        f.func = NoiseReduction_Funct;
                        break;

                    case "Threshold":
                        f.func = ThresholdFunct;
                        break;

                    case "Histogram":
                        f.func = HistogramFunct;
                        break;

                    case "Blur":
                        f.func = BlurFunct;
                        break;

                    case "Gaussian blur":
                        f.func = GaussianBlurFunct; 
                        break;

                    case "Hough circles":
                        f.func = HoughCirclesFunct; 
                        break;

                    case "Filter Features by Size":
                        f.func = FilterFeaturesBySizeFunct;
                        break;

                    default:
                        continue;
                        // break; 
                }
                f.parameter_int = UIfucnt.parameterInt;
                f.parameter_double = UIfucnt.parameterDouble;
                f.R = UIfucnt.R;
                f.B = UIfucnt.B;
                f.G = UIfucnt.G;
                f.parameter_doubleA = UIfucnt.parameterDoubleA;
                f.parameter_doubleB = UIfucnt.parameterDoubleB;
                f.parameter_doubleC = UIfucnt.parameterDoubleC;
                NewList.Add(f);
                MainForm.DisplayText(UIfucnt.Name + ", " + f.parameter_int.ToString() + ", " + f.parameter_double.ToString() + ", "
                    + f.R.ToString() + ", " + f.G.ToString() + ", " + f.B.ToString() + ", "
                    + f.parameter_doubleA.ToString() + ", " + f.parameter_doubleB.ToString() + ", " + f.parameter_doubleC.ToString());
            };
            return NewList;
        }

        // So that function "Jog before measurement" asks only once per measurement
        public bool JoggingRequested = false;

        // ==========================================================================================================
        // For display: Get the function list, transfer to video processing

        public void BuildDisplayFunctionsList(List<AForgeFunctionDefinition> UiList)
        {
            List<AForgeFunction> NewList = BuildFunctionsList(UiList);    // Get the list
            int tries = 0;
            // Stop video
            bool pause = PauseProcessing;
            if (VideoSource != null)
            {
                if (ReceivingFrames)
                {
                    // stop video
                    PauseProcessing = true;  // ask for stop
                    Paused = false;
                    while (!Paused)
                    {
                        Thread.Sleep(10);  // wait until really stopped
                        tries++;
                        if (tries > 40)
                        {
                            break;
                        }
                    };
                }
            }
            // copy new list
            DisplayFunctions.Clear();
            DisplayFunctions = NewList;
            Thread.Sleep(50);  // wait until really stopped
            PauseProcessing = pause;  // restart video is it was running
        }

        public void ClearDisplayFunctionsList()
        {
            if (DisplayFunctions.Count == 0)
            {
                return;
            }
            // Stop video
            bool pause = PauseProcessing;
            int tries = 0;
            if (VideoSource != null)
            {
                if (ReceivingFrames)
                {
                    // stop video
                    PauseProcessing = true;  // ask for stop
                    Paused = false;
                    while (!Paused)
                    {
                        Thread.Sleep(10);  // wait until really stopped
                        tries++;
                        if (tries>40)
                        {
                            break;
                        }
                    };
                }
            }
            DisplayFunctions.Clear();
            PauseProcessing = pause;  // restart video is it was running
        }

        // ==========================================================================================================
        // Members we need for our drawing functions
        // ==========================================================================================================

        // ==========================================================================================================
        // Zoom
        public bool Zoom { get; set; }          // If image is zoomed or not
        private double _ZoomFactor = 1.0;
        public double ZoomFactor                // If it is, this much
        {
            get
            {
                return _ZoomFactor;
            }
            set
            {
                _ZoomFactor = value;
            }
        }

        public int Threshold { get; set; }                  // Threshold for all the "draw" functions
        public bool GrayScale { get; set; }                 // If image is converted to grayscale 
        public bool Invert { get; set; }                    // If image is inverted (makes most sense on grayscale, looking for black stuff on light background)
        public bool DrawCross { get; set; }         // If crosshair cursor is drawn
        public bool DrawArrow { get; set; }         // If arrow is drawn
        public double ArrowAngle { get; set; }      // to which angle
        public bool DrawSidemarks { get; set; }     // If marks on the side of the image are drawn
        public double SideMarksX { get; set; }      // How many marks on top and bottom (X) and sides (Y)
        public double SideMarksY { get; set; }      // (double, so you can do "SidemarksX= workarea_in_mm / 100;" to get mark every 10cm
        public bool DrawDashedCross { get; set; }   // If a dashed crosshaircursor is drawn (so that the center remains visible)
        public bool DrawGrid { get; set; }          // Draws aiming grid for parts alignment
        public bool FindCircles { get; set; }       // Find and highlight circles in the image
        public bool FindRectangles { get; set; }    // Find and draw regtangles in the image
        public bool FindComponentByOutlines { get; set; }     // Finds a component and identifies its center, using its outline
        public bool FindComponentByPads { get; set; }     // Finds a component and identifies its center, using its pads
        public bool Draw_Snapshot { get; set; }     // Draws the snapshot on the image 
        public bool PauseProcessing { get; set; }   // Drawing the video slows everything down. This can pause it for measurements.
        public bool Paused = true;                 // set in video processing indicating it is safe to change processing function list
        public bool TestAlgorithm { get; set; }
        public bool DrawBox { get; set; }           // Draws a box on the image that is used for scale setting
        public bool ShowProcessing = true;   // If processing is on, shows the processed image; if false, shows results on top of unprocessed image

        private int boxSizeX;
        public int BoxSizeX                         // The box size
        {
            get
            {
                return (boxSizeX);
            }
            set
            {
                boxSizeX = value;
                BoxRotationDeg = boxRotation; // force recalculation of corner points

            }
        }
        private int boxSizeY;
        public int BoxSizeY
        {
            get
            {
                return (boxSizeY);
            }
            set
            {
                boxSizeY = value;
                BoxRotationDeg = boxRotation; // force recalculation of corner points

            }
        }

        private double boxRotation = 0;
        private double boxRotationRad = 0;
        private System.Drawing.Point[] BoxPoints = new System.Drawing.Point[4];
        public double BoxRotationDeg        // The box is drawn rotated this much
        {
            get
            {
                return (boxRotation);
            }
            set
            {
                boxRotation = value;
                // Calculate corner points
                BoxPoints[0].X = BoxSizeX / 2;
                BoxPoints[0].Y = BoxSizeY / 2;
                BoxPoints[1].X = -BoxSizeX / 2;
                BoxPoints[1].Y = BoxSizeY / 2;
                BoxPoints[2].X = -BoxSizeX / 2;
                BoxPoints[2].Y = -BoxSizeY / 2;
                BoxPoints[3].X = BoxSizeX / 2;
                BoxPoints[3].Y = -BoxSizeY / 2;
                // now, rotate them:
                boxRotationRad = -boxRotation / (180 / Math.PI);  // to radians, and counter-clockwise
                for (int i = 0; i < 4; i++)
                {
                    // If you rotate point (px, py) around point (ox, oy) by angle theta you'll get:
                    // p'x = cos(theta) * (px-ox) - sin(theta) * (py-oy) + ox
                    // p'y = sin(theta) * (px-ox) + cos(theta) * (py-oy) + oy
                    // ox, oy= 0 ==>
                    // p'x = cos(theta) * (px) - sin(theta) * (py)
                    // p'y = sin(theta) * (px) + cos(theta) * (py)
                    double pX = BoxPoints[i].X;
                    double pY = BoxPoints[i].Y;
                    BoxPoints[i].X = (int)Math.Round(Math.Cos(boxRotationRad) * pX - Math.Sin(boxRotationRad) * pY);
                    BoxPoints[i].Y = (int)Math.Round(Math.Sin(boxRotationRad) * pX + Math.Cos(boxRotationRad) * pY);
                }
            }
        }



        // =========================================================
        // Convert list of AForge.NET's points to array of .NET points
        private System.Drawing.Point[] ToPointsArray(List<IntPoint> points)
        {
            System.Drawing.Point[] array = new System.Drawing.Point[points.Count];

            for (int i = 0, n = points.Count; i < n; i++)
            {
                array[i] = new System.Drawing.Point(points[i].X, points[i].Y);
            }

            return array;
        }


        // ==========================================================================================================
        // ==========================================================================================================

        // Each frame goes through Video_NewFrame()

        // ==========================================================================================================
        // ==========================================================================================================

        Bitmap frame;
        static int CollectorCount = 0;
        static int ErrorCount = 0;

        private void Video_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            if (eventArgs.Frame==null)
            {
                if (ErrorCount==0)
                {
                    MainForm.DisplayText("Video_NewFrame, null frame.", KnownColor.DarkRed, true);
                }
                ErrorCount++;
                if (ErrorCount>200)
                {
                    ErrorCount = 0;
                }
            }

            ReceivingFrames = true;

            // Take a copy for measurements, if needed:
            if (CopyFrame)
            {
                if (TemporaryFrame != null)
                {
                    TemporaryFrame.Dispose();
                }
                TemporaryFrame = (Bitmap)eventArgs.Frame.Clone();
                CopyFrame = false;
            };

            if (PauseProcessing)
            {
                Paused = true;
                return;
            };

            if (!Active)
            {
                return;
            }


            // Working with a copy of the frame to avoid conflict.  Protecting the region where the copy is made
            lock (ProtectedPictureBox._locker)
            {
                frame = (Bitmap)eventArgs.Frame.Clone();
            }

            if (DisplayFunctions != null)
            {
                if (DisplayFunctions.Count!=0)
                {
                    // Selection is either show processing or show results
                    // Even with safeguards, changing DisplayFunctions can cause errors. 
                    try
                    {
                        if (ShowProcessing)  // Process the displayed frame and also draw on it
                        {
                            foreach (AForgeFunction f in DisplayFunctions)
                            {
                                f.func(ref frame, f.parameter_int, f.parameter_double, f.R, f.G, f.B,
                                    f.parameter_doubleA, f.parameter_doubleB, f.parameter_doubleC);
                            }
                            if (FindCircles)
                            {
                                List<Shapes.Circle> Circles = FindCirclesFunct(frame);
                                DrawCirclesFunct(ref frame, Circles, 1.0);
                            }
                            if (FindRectangles)
                            {
                                List<Shapes.Rectangle> Rectangles = FindRectanglesFunct(frame);
                                DrawRectanglesFunct(ref frame, Rectangles, 1.0);
                            }
                            if (FindComponentByOutlines)
                            {
                                List<Shapes.Component> Components = FindComponentsFromOutline_Funct(frame);
                                DrawComponentsFunct(ref frame, Components, 1.0);
                            }
                            if (FindComponentByPads)
                            {
                                List<Shapes.Component> Components = FindComponentsFromPads_Funct(frame);
                                DrawComponentsFunct(ref frame, Components, 1.0);
                            }
                        }
                        else
                        {
                            Bitmap ProcessedFrame = (Bitmap)frame.Clone();  // Process a copy and find features on it; draw on displayed frame
                            foreach (AForgeFunction f in DisplayFunctions)
                            {
                                f.func(ref ProcessedFrame, f.parameter_int, f.parameter_double, f.R, f.G, f.B,
                                    f.parameter_doubleA, f.parameter_doubleB, f.parameter_doubleC);
                            }
                            double zoom = 1 / GetDisplayZoom();  // If measurement frame is zoomed, we need to zoom the displayed results as well
                            if (FindCircles)
                            {
                                List<Shapes.Circle> Circles = FindCirclesFunct(ProcessedFrame);
                                DrawCirclesFunct(ref frame, Circles, zoom);
                            }
                            if (FindRectangles)
                            {
                                List<Shapes.Rectangle> Rectangles = FindRectanglesFunct(ProcessedFrame);
                                DrawRectanglesFunct(ref frame, Rectangles, zoom);
                            }
                            if (FindComponentByOutlines)
                            {
                                List<Shapes.Component> Components = FindComponentsFromOutline_Funct(ProcessedFrame);
                                DrawComponentsFunct(ref frame, Components, zoom);
                            }
                            if (FindComponentByPads)
                            {
                                List<Shapes.Component> Components = FindComponentsFromPads_Funct(ProcessedFrame);
                                DrawComponentsFunct(ref frame, Components, zoom);
                            }
                        }
                    }
                    catch (System.InvalidOperationException)
                    {
                        // No need to do anything, next frame fixes it
                    }
                }
            }

            if (Mirror)
            {
                frame = MirrorFunct(ref frame);
            };

            if (Zoom)
            {
                ZoomFunct(ref frame, ZoomFactor);
            };

            if (DrawBox)
            {
                DrawBoxFunct(ref frame);
            };

            if (DrawCross)
            {
                DrawCrossFunct(ref frame);
            };

            if (DrawSidemarks)
            {
                DrawSidemarksFunct(ref frame);
            };

            if (DrawDashedCross)
            {
                DrawDashedCrossFunct(ref frame);
            };

            if (DrawGrid)
            {
                DrawGridFunct(ref frame);
            };

            if (DrawArrow)
            {
                DrawArrowFunct(ref frame, FrameCenterX, FrameCenterY, 60);
            };

            lock (ProtectedPictureBox._locker)
            {
                if (ImageBox.Image != null)
                {
                    ImageBox.Image.Dispose();
                }
                ImageBox.Image = (Bitmap)frame.Clone();
            }

            frame.Dispose();
            if (CollectorCount > 20)
            {
                GC.Collect();
                CollectorCount = 0;
            }
            else
            {
                CollectorCount++;
            }
        } // end Video_NewFrame

        // see http://www.codeproject.com/Questions/689320/object-is-currently-in-use-elsewhere about the locker

        // ==========================================================================================================
        // Functions compatible with lists:
        // ==========================================================================================================
        // Note, that each function needs to keep the image in RGB, otherwise drawing fill fail 

        // =========================================================  
        private void FilterFeaturesBySizeFunct(ref Bitmap frame, int par_int, double par_d, int par_R, int par_G, int par_B,
            double par_dA, double par_dB, double par_dC)
        {
            // Find limits
            int MinSize = Convert.ToInt32(par_dA / XmmPerPixel);
            int MaxSize = Convert.ToInt32(par_dB / XmmPerPixel);
            // create filter
            BlobsFiltering filter = new BlobsFiltering();
            // configure filter
            filter.CoupledSizeFiltering = true;
            filter.MinWidth = MinSize;
            filter.MinHeight = MinSize;
            filter.MaxHeight = MaxSize;
            filter.MaxWidth = MaxSize;
            // apply the filter
            filter.ApplyInPlace(frame);
        }

        private void NoiseReduction_Funct(ref Bitmap frame, int par_int, double par_d, int par_R, int par_G, int par_B,
            double par_dA, double par_dB, double par_dC)
        {
            frame = Grayscale.CommonAlgorithms.RMY.Apply(frame);    // Make gray
            switch (par_int)
            {
                case 1:
                    BilateralSmoothing Bil_filter = new BilateralSmoothing();
                    Bil_filter.KernelSize = 7;
                    Bil_filter.SpatialFactor = 10;
                    Bil_filter.ColorFactor = 30;
                    Bil_filter.ColorPower = 0.5;
                    Bil_filter.ApplyInPlace(frame);
                    break;

                case 2:
                    Median M_filter = new Median();
                    M_filter.ApplyInPlace(frame);
                    break;

                case 3:
                    Mean Meanfilter = new Mean();
                    // apply the MirrFilter
                    Meanfilter.ApplyInPlace(frame);
                    break;

                default:
                    Median Median_filter = new Median();
                    Median_filter.ApplyInPlace(frame);
                    break;
            }
            GrayscaleToRGB RGBfilter = new GrayscaleToRGB();    // back to color format
            frame = RGBfilter.Apply(frame);
        }

        // =========================================================
        private void Edge_detectFunc(ref Bitmap frame, int par_int, double par_d, int par_R, int par_G, int par_B,
            double par_dA, double par_dB, double par_dC)
        {
            frame = Grayscale.CommonAlgorithms.RMY.Apply(frame);    // Make gray
            switch (par_int)
            {
                case 1:
                    SobelEdgeDetector SobelFilter = new SobelEdgeDetector();
                    SobelFilter.ApplyInPlace(frame);
                    break;

                case 2:
                    DifferenceEdgeDetector DifferenceFilter = new DifferenceEdgeDetector();
                    DifferenceFilter.ApplyInPlace(frame);
                    break;

                case 3:
                    HomogenityEdgeDetector HomogenityFilter = new HomogenityEdgeDetector();
                    HomogenityFilter.ApplyInPlace(frame);
                    break;

                case 4:
                    CannyEdgeDetector Cannyfilter = new CannyEdgeDetector();
                    // apply the MirrFilter
                    Cannyfilter.ApplyInPlace(frame);
                    break;

                default:
                    HomogenityEdgeDetector filter = new HomogenityEdgeDetector();
                    filter.ApplyInPlace(frame);
                    break;
            }
            GrayscaleToRGB RGBfilter = new GrayscaleToRGB();    // back to color format
            frame = RGBfilter.Apply(frame);
        }

        // =========================================================
        private void InvertFunct(ref Bitmap frame, int par_int, double par_d, int par_R, int par_G, int par_B,
            double par_dA, double par_dB, double par_dC)
        {
            Invert filter = new Invert();
            filter.ApplyInPlace(frame);
        }

        // =========================================================
        private void ErosionFunct(ref Bitmap frame, int par_int, double par_d, int par_R, int par_G, int par_B,
            double par_dA, double par_dB, double par_dC)
        {
            Erosion filter = new Erosion();
            filter.ApplyInPlace(frame);
        }

        // =========================================================
        private void HistogramFunct(ref Bitmap frame, int par_int, double par_d, int par_R, int par_G, int par_B,
            double par_dA, double par_dB, double par_dC)
        {
            // create MirrFilter
            HistogramEqualization filter = new HistogramEqualization();
            // process image
            filter.ApplyInPlace(frame);
        }


        // =========================================================
        private void BlurFunct(ref Bitmap frame, int par_int, double par_d, int par_R, int par_G, int par_B,
            double par_dA, double par_dB, double par_dC)
        {
            // create filter
            Blur filter = new Blur();
            // apply the filter
            filter.ApplyInPlace(frame);
        }


        // =========================================================
        private void GaussianBlurFunct(ref Bitmap frame, int par_int, double par_d, int par_R, int par_G, int par_B,
            double par_dA, double par_dB, double par_dC)
        {
            // create filter with kernel size equal to 11
            GaussianBlur filter = new GaussianBlur(par_d, 11);
            // apply the filter
            filter.ApplyInPlace(frame);
        }

        private void HoughCirclesFunct(ref Bitmap frame, int par_int, double par_d, int par_R, int par_G, int par_B,
            double par_dA, double par_dB, double par_dC)
        {
            HoughCircleTransformation circleTransform = new HoughCircleTransformation(2* par_int);
            // apply Hough circle transform
            frame = Grayscale.CommonAlgorithms.RMY.Apply(frame);    // Make gray
            circleTransform.ProcessImage(frame);
            frame = circleTransform.ToBitmap();
            GrayscaleToRGB RGBfilter = new GrayscaleToRGB();    // back to color format
            frame = RGBfilter.Apply(frame);

            // get circles using relative intensity
            //HoughCircle[] circles = circleTransform.GetCirclesByRelativeIntensity(par_d);
        }


        // =========================================================
        private void KillColor_Func(ref Bitmap frame, int par_int, double par_d, int par_R, int par_G, int par_B,
            double par_dA, double par_dB, double par_dC)
        {
            // create MirrFilter
            EuclideanColorFiltering filter = new EuclideanColorFiltering();
            // set center colol and radius
            filter.CenterColor = new RGB((byte)par_R, (byte)par_G, (byte)par_B);
            filter.Radius = (short)par_int;
            filter.FillOutside = false;
            // apply the MirrFilter
            filter.ApplyInPlace(frame);
        }

        // =========================================================
        private void KeepColor_Func(ref Bitmap frame, int par_int, double par_d, int par_R, int par_G, int par_B,
            double par_dA, double par_dB, double par_dC)
        {
            // create MirrFilter
            EuclideanColorFiltering filter = new EuclideanColorFiltering();
            // set center colol and radius
            filter.CenterColor = new RGB((byte)par_R, (byte)par_G, (byte)par_B);
            filter.Radius = (short)par_int;
            filter.FillOutside = true;
            // apply the MirrFilter
            filter.ApplyInPlace(frame);
        }

        // =========================================================
        private void ThresholdFunct(ref Bitmap frame, int par_int, double par_d, int par_R, int par_G, int par_B,
            double par_dA, double par_dB, double par_dC)
        {
            frame = Grayscale.CommonAlgorithms.RMY.Apply(frame);
            Threshold filter = new Threshold(par_int);
            filter.ApplyInPlace(frame);
            GrayscaleToRGB toColFilter = new GrayscaleToRGB();
            frame = toColFilter.Apply(frame);
        }


        // =========================================================
        private void GrayscaleFunc(ref Bitmap frame, int par_int, double par_d, int par_R, int par_G, int par_B,
            double par_dA, double par_dB, double par_dC)
        {
            Grayscale toGrFilter = new Grayscale(0.2125, 0.7154, 0.0721);       // create grayscale MirrFilter (BT709)
            Bitmap fr = toGrFilter.Apply(frame);
            GrayscaleToRGB toColFilter = new GrayscaleToRGB();
            frame = toColFilter.Apply(fr);
        }

        // =========================================================
        private void Contrast_scretchFunc(ref Bitmap frame, int par_int, double par_d, int par_R, int par_G, int par_B,
            double par_dA, double par_dB, double par_dC)
        {
            ContrastStretch filter = new ContrastStretch();
            // process image
            filter.ApplyInPlace(frame);
        }

        // =========================================================
        private void Meas_ZoomFunc(ref Bitmap frame, int par_int, double par_d, int par_R, int par_G, int par_B,
            double par_dA, double par_dB, double par_dC)
        {
            ZoomFunct(ref frame, par_d);
        }

        private void ManualJogFunc()
        {
            //if (MainForm.LastTabPage == "Algorithms_tabPage")
            //{
            //    return;
            //}
            if (!JoggingRequested)
            {
                return;
            }
            string answer = MainForm.NonModalMessageBox(
                "Jog machine to position and click Continue.", "Manual measurement location",
                "", "", "Continue");
            JoggingRequested= false;
        }

        // ==========================================================================================================
        // Components, newer version:
        // ==========================================================================================================
        // helpers:

        // ===========
        private List<IntPoint> GetBlobsOutline(BlobCounter blobCounter, Blob blob)
        {
            List<IntPoint> leftPoints, rightPoints, edgePoints = new List<IntPoint>();
            // get blob's edge points
            blobCounter.GetBlobsLeftAndRightEdges(blob,
                out leftPoints, out rightPoints);

            edgePoints.AddRange(leftPoints);
            edgePoints.AddRange(rightPoints);
            return edgePoints;
        }

        // ===========
        private List<IntPoint> GetConvexHull(List<IntPoint> edgePoints)
        {
            // create convex hull searching algorithm
            GrahamConvexHull hullFinder = new GrahamConvexHull();
            ClosePointsMergingOptimizer optimizer1 = new ClosePointsMergingOptimizer();
            FlatAnglesOptimizer optimizer2 = new FlatAnglesOptimizer();
            List<IntPoint> Outline = hullFinder.FindHull(edgePoints);
            optimizer1.MaxDistanceToMerge = 4;
            optimizer2.MaxAngleToKeep = 170F;
            Outline = optimizer2.OptimizeShape(Outline);
            Outline = optimizer1.OptimizeShape(Outline);
            if (Outline.Count < 3)
            {
                return null;
            }
            return Outline;
        }

        // ===========
        private double GetAngle(IntPoint p1, IntPoint p2)
        {
            // returns the angle between line (p1,p2) and horizontal axis, in degrees
            double A = 0;
            if (p2.X == p1.X)
            {
                if (p2.Y > p1.Y)
                {
                    return 90;
                }
                else
                {
                    return 270;
                }
            }
            else
            {
                A = Math.Atan(Math.Abs((double)(p1.Y - p2.Y) / (double)(p1.X - p2.X)));
                A = A * 180.0 / Math.PI; // in deg.
            }
            // quadrants: A is now first quadrant solution
            if ((p1.X < p2.X) && (p1.Y <= p2.Y))
            {
                return A;   // 1st q.
            }
            else if ((p1.X > p2.X) && (p1.Y < p2.Y))
            {
                return A + 90; // 2nd q.
            }
            else if ((p1.X > p2.X) && (p1.Y >= p2.Y))
            {
                return A + 180; // 3rd q.
            }
            else
            {
                return 360 - A; // 4th q.
            }
        }

        // ===========
        private double RectangleArea(IntPoint p1, IntPoint p2)
        {
            return Math.Abs(((double)p2.X - (double)p1.X) * ((double)p2.Y - (double)p1.Y));
        }

        // ===========
        private List<IntPoint> ScaleOutline(Double scale, List<IntPoint> Outline)
        {
            List<IntPoint> Result = new List<IntPoint>();
            foreach (var p in Outline)
            {
                Result.Add(new IntPoint((int)(p.X * scale), (int)(p.Y * scale)));
            }
            return Result;
        }
        // ===========
        private IntPoint RotatePoint(double angle, IntPoint p, IntPoint o)
        {
            // TODO: put all rotations in one routine

            // If you rotate point (px, py) around point (ox, oy) by angle theta you'll get:
            // p'x = cos(theta) * (px-ox) - sin(theta) * (py-oy) + ox
            // p'y = sin(theta) * (px-ox) + cos(theta) * (py-oy) + oy

            double theta = angle * (Math.PI / 180.0); // to radians
            IntPoint Pout = new IntPoint();
            Pout.X = Convert.ToInt32(Math.Cos(theta) * (p.X - o.X) - Math.Sin(theta) * (p.Y - o.Y) + o.X);
            Pout.Y = Convert.ToInt32(Math.Sin(theta) * (p.X - o.X) + Math.Cos(theta) * (p.Y - o.Y) + o.Y);
            return Pout;
        }

        // ===========
        private List<IntPoint> RotateOutline(double theta, List<IntPoint> Outline, IntPoint RotOrigin)
        {
            // returns the outline, rotated by angle around RotOrigin
            List<IntPoint> Result = new List<IntPoint>();
            IntPoint Pout = new IntPoint();
            foreach (var p in Outline)
            {
                Pout = RotatePoint(theta, p, RotOrigin);
                Result.Add(Pout);
            }
            return Result;
        }

        // ===========
        private List<IntPoint> GetMinimumBoundingRectangle(List<IntPoint> Outline)
        {
            // Using caliber algorithm to find the minimum bounding regtangle
            // (see http://www.datagenetics.com/blog/march12014/index.html):

            // For each line segment,
            // Rotate the hull so that the segment is horizontal.
            // Get the hull bounding rectangle
            // Measure the area
            // If first iteration or the area is smaller than the previous one,
            // store the area, the rectangle and the rotation with point
            // The solution is the stored rectangle, rotated back to original orientation.

            bool first = true;
            IntPoint minXY = new IntPoint();
            IntPoint maxXY = new IntPoint();
            double SmallestArea = 0;
            double AngleOfsolution = 0;
            IntPoint SolutionMin = new IntPoint();
            IntPoint SolutionMax = new IntPoint();
            IntPoint SolutionOrg = new IntPoint();

            for (int i = 1; i < Outline.Count; i++) // For each line segment,
            {
                double angle = GetAngle(Outline[i - 1], Outline[i]);
                List<IntPoint> RotatedOutline = RotateOutline(-angle, Outline, Outline[i - 1]); // Rotate the hull so that the segment is horizontal.
                PointsCloud.GetBoundingRectangle(RotatedOutline, out minXY, out maxXY);    // Get the hull bounding rectangle
                double area = RectangleArea(minXY, maxXY);
                if (first || (area < SmallestArea))
                {
                    SmallestArea = area;        // store the area, the rectangle and the rotation
                    AngleOfsolution = angle;
                    SolutionMin = minXY;
                    SolutionMax = maxXY;
                    SolutionOrg = Outline[i - 1];
                    first = false;
                }
            }
            List<IntPoint> rect = new List<IntPoint>() { SolutionMin, new IntPoint(SolutionMax.X, SolutionMin.Y),
                SolutionMax ,new IntPoint(SolutionMin.X, SolutionMax.Y) };
            rect = RotateOutline(AngleOfsolution, rect, SolutionOrg); // stored rectangle, rotated back to original orientation and place
            return rect;
        }

        // ===========
        private List<Shapes.Component> FindComponentsFromPads_Funct(Bitmap bitmap)
        {
            // Locating objects
            BlobCounter blobCounter = new BlobCounter();
            blobCounter.FilterBlobs = true;
            blobCounter.MinHeight = 2;
            blobCounter.MinWidth = 2;
            blobCounter.ProcessImage(bitmap);
            Blob[] blobs = blobCounter.GetObjectsInformation(); // Get blobs
            List<IntPoint> edgePoints = new List<IntPoint>(); 
            foreach (Blob blob in blobs)    // and merge their outlines to one list
            {
                if ((blob.Rectangle.Height > (bitmap.Size.Height-5)) || 
                    (blob.Rectangle.Width > (bitmap.Size.Width - 5)))
                {
                    continue;  // The whole image could be a blob, discard that
                }
                else
                {
                    edgePoints.AddRange(GetBlobsOutline(blobCounter, blob));     // get edge points, add to list
                }
            }

            List<Shapes.Component> Components = new List<Shapes.Component>();
            if (edgePoints.Count==0)
            {
                return Components;  // did not get an outline, return empty list
            };

            List<IntPoint> Outline = GetConvexHull(edgePoints); // convert to convex hull
            if (Outline==null)  //
            {
                return Components;
            }
            Outline.Add(Outline[0]);  // creates line segment from last hull point to stat, closing the outline
            if (Outline.Count < 3)
            {
                return Components;  // did not get an outline, return empty list
            }
            List<IntPoint> Box = GetMinimumBoundingRectangle(Outline);
            Components.Add(new Shapes.Component(Box));
            return Components;
        }

        // =========== 
        private List<Shapes.Component> FindComponentsFromOutline_Funct(Bitmap bitmap)
        {
            // Locating objects
            BlobCounter blobCounter = new BlobCounter();
            blobCounter.FilterBlobs = true;
            blobCounter.MinHeight = 8;
            blobCounter.MinWidth = 8;
            blobCounter.ProcessImage(bitmap);
            List<Shapes.Component> Components = new List<Shapes.Component>();

            Blob[] blobs = blobCounter.GetObjectsInformation(); // Get blobs
            foreach (Blob blob in blobs)
            {
                if ((blob.Rectangle.Height > (bitmap.Height-10)) && (blob.Rectangle.Width > (bitmap.Height - 10)))
                {
                    break;  // The whole image could be a blob, discard that
                }

                List<IntPoint> edgePoints = GetBlobsOutline(blobCounter, blob);     // get edge points
                List<IntPoint> OutlineRaw = GetConvexHull(edgePoints);                 // convert to convex hull
                if (OutlineRaw.Count < 3)
                {
                    break;
                }
                // add start point to list, so we get line segments
                OutlineRaw.Add(OutlineRaw[0]);
                // We are dealing with small objects, one pixel is coarse. Therefore, all calculations are done
                // on a scaled outline, scaling back after finding the rectangle. (This is because AForge uses IntPoints.)
                List<IntPoint> Outline = ScaleOutline(1.0, OutlineRaw);
                List<IntPoint> Box = GetMinimumBoundingRectangle(Outline);    // get bounding rectangle
                //Box= ScaleOutline(0.001, Box);  // scale back
                if (Box.Count != 4)
                {
                    MainForm.DisplayText("Rectangle with " + Box.Count.ToString() + "corners (BoxToComponent)", KnownColor.Red, true);
                }
                else
                {
                    Components.Add(new Shapes.Component(Box));
                }
            }
            return Components;
        }

        // ===========
        private void DrawComponentsFunct(ref Bitmap bitmap, List<Shapes.Component> Components, double zoom)
        {
            int PenSize = (int)(3 * zoom);
            if (PenSize < 2)
            {
                PenSize = 2;
            }
            Graphics g = Graphics.FromImage(bitmap);
            Pen OrangePen = new Pen(Color.DarkOrange, 3);
            for (int i = 0; i < Components.Count; i++)
            {
                List<System.Drawing.PointF> Corners = new List<System.Drawing.PointF>();

                for (int c = 0; c < Components[i].BoundingBox.Corners.Count; c++)
                {
                    double X = Components[i].BoundingBox.Corners[c].X;
                    X = X - FrameCenterX;
                    X = X * zoom;
                    X = X + FrameCenterX;
                    double Y = Components[i].BoundingBox.Corners[c].Y;
                    Y = Y - FrameCenterY;
                    Y = Y * zoom;
                    Y = Y + FrameCenterY;
                    System.Drawing.PointF Pnt = new System.Drawing.PointF();
                    Pnt.X = (float)X;
                    Pnt.Y = (float)Y;
                    Corners.Add(Pnt);
                }
                g.DrawPolygon(OrangePen, Corners.ToArray());
                double Xc = Components[i].Center.X;
                Xc = Xc - FrameCenterX;
                Xc = Xc * zoom;
                Xc = Xc + FrameCenterX;
                double Yc = Components[i].Center.Y;
                Yc = Yc - FrameCenterY;
                Yc = Yc * zoom;
                Yc = Yc + FrameCenterY;
                ArrowAngle = Components[i].Angle;
                DrawArrowFunct(ref bitmap, (int)Xc, (int)Yc, (int)(Components[i].Xsize * 0.3));
            }
            g.Dispose();
            OrangePen.Dispose();


            /*

            Graphics g = Graphics.FromImage(bitmap);
            Pen OrangePen = new Pen(Color.DarkOrange, 3);
            for (int i = 0, n = Components.Count; i < n; i++)
            {
                g.DrawPolygon(OrangePen, ToPointsArray(Components[i].BoundingBox.Corners));
                ArrowAngle = Components[i].Angle;
                DrawArrowFunct(ref bitmap, (int)Components[i].Center.X, (int)Components[i].Center.Y, (int)(Components[i].Xsize*0.3));
            }
            g.Dispose();
            OrangePen.Dispose();
            */
        }

        // ==========================================================================================================
        // Circles:
        // ==========================================================================================================
        private List<Shapes.Circle> FindCirclesFunct(Bitmap bitmap)
        {
            // locating objects
            BlobCounter blobCounter = new BlobCounter();
            blobCounter.FilterBlobs = true;
            blobCounter.MinHeight = 5;
            blobCounter.MinWidth = 5;
            blobCounter.ProcessImage(bitmap);
            Blob[] blobs = blobCounter.GetObjectsInformation();

            List<Shapes.Circle> Circles = new List<Shapes.Circle>();

            for (int i = 0, n = blobs.Length; i < n; i++)
            {
                SimpleShapeChecker shapeChecker = new SimpleShapeChecker();
                List<IntPoint> edgePoints = blobCounter.GetBlobsEdgePoints(blobs[i]);
                AForge.Point center;
                float radius;

                // is circle ?
                if (shapeChecker.IsCircle(edgePoints, out center, out radius))
                {
                    if (radius > 2)  // Filter out some noise
                    {
                        Circles.Add(new Shapes.Circle(center, radius));
                    }
                }
            }
            return (Circles);
        }

        // =========================================================
        private void DrawCirclesFunct(ref Bitmap bitmap, List<Shapes.Circle> Circles, double zoom)
        {

            if (Circles.Count == 0)
            {
                return;
            }

            int PenSize = (int)(3 * zoom);
            if (PenSize<2)
            {
                PenSize = 2;
            }

            Graphics g = Graphics.FromImage(bitmap);
            Pen LimePen = new Pen(Color.Lime, PenSize);
            for (int i = 0, n = Circles.Count; i < n; i++)
            {
                double X = Circles[i].Center.X;
                X = X - Circles[i].Radius;
                X = X - FrameCenterX;
                X = X * zoom;
                X = X + FrameCenterX;
                double Y = Circles[i].Center.Y;
                Y = Y - Circles[i].Radius;
                Y = Y - FrameCenterY;
                Y = Y * zoom;
                Y = Y + FrameCenterY;
                float dia = (float)((Circles[i].Radius * 2 * zoom));
                g.DrawEllipse(LimePen, (float)X, (float)Y, dia, dia);
            }
            g.Dispose();
            LimePen.Dispose();
        }

        // ==========================================================================================================
        // Rectangles:
        // ==========================================================================================================
        private List<Shapes.Rectangle> FindRectanglesFunct(Bitmap bitmap)
        {
            // step 1 - turn background to black (done)

            // step 2 - locating objects
            BlobCounter blobCounter = new BlobCounter();
            blobCounter.FilterBlobs = true;
            blobCounter.MinHeight = 3;
            blobCounter.MinWidth = 3;
            blobCounter.ProcessImage(bitmap);
            Blob[] blobs = blobCounter.GetObjectsInformation();

            // step 3 - check objects' type and do what you do:
            List<Shapes.Rectangle> Rectangles = new List<Shapes.Rectangle>();

            for (int i = 0, n = blobs.Length; i < n; i++)
            {
                SimpleShapeChecker QuadChecker = new SimpleShapeChecker();
                List<IntPoint> edgePoints = blobCounter.GetBlobsEdgePoints(blobs[i]);
                List<IntPoint> cornerPoints;

                // fine tune ShapeChecker
                QuadChecker.AngleError = 7;  // default 7
                QuadChecker.LengthError = 0.1F;  // default 0.1 (10%)
                QuadChecker.MinAcceptableDistortion = 0.5F;  // in pixels, default 0.5 
                QuadChecker.RelativeDistortionLimit = 0.05F;  // default 0.03 (3%)

                // use the Outline checker to extract the corner points
                if (QuadChecker.IsQuadrilateral(edgePoints, out cornerPoints))
                {
                    // only do things if the corners form a rectangle
                    SimpleShapeChecker RectangleChecker = new SimpleShapeChecker();
                    RectangleChecker.AngleError = 7;  // default 7
                    RectangleChecker.LengthError = 0.050F;  // default 0.1 (10%)
                    RectangleChecker.MinAcceptableDistortion = 1F;  // in pixels, default 0.5 
                    RectangleChecker.RelativeDistortionLimit = 0.05F;  // default 0.03 (3%)
                    if (RectangleChecker.CheckPolygonSubType(cornerPoints) == PolygonSubType.Rectangle)
                    //if (true)
                    {
                        List<IntPoint> corners = PointsCloud.FindQuadrilateralCorners(edgePoints);
                        // In some cases, the bitmap edges count as a rectangle. That should be ignored:
                        if ( !( (corners[2].X==(bitmap.Size.Width-1)) && 
                                (corners[2].Y == (bitmap.Size.Height - 1))
                              )
                            )
                        {
                            Rectangles.Add(new Shapes.Rectangle(corners));
                        }
                    }
                }
            }
            return (Rectangles);
        }
        // =========================================================
        private void DrawRectanglesFunct(ref Bitmap image, List<Shapes.Rectangle> RectanglesIn, double zoom)
        {
            int PenSize = (int)(3 * zoom);
            if (PenSize < 2)
            {
                PenSize = 2;
            }
            Graphics g = Graphics.FromImage(image);
            Pen LimePen = new Pen(Color.Lime, PenSize);
            for (int i = 0; i < RectanglesIn.Count; i++)
            {
                List<System.Drawing.PointF> Corners = new List<System.Drawing.PointF>();

                for (int c = 0; c < RectanglesIn[i].Corners.Count; c++)
                {
                    double X = RectanglesIn[i].Corners[c].X;
                    X = X - FrameCenterX;
                    X = X * zoom;
                    X = X + FrameCenterX;
                    double Y = RectanglesIn[i].Corners[c].Y;
                    Y = Y - FrameCenterY;
                    Y = Y * zoom;
                    Y = Y + FrameCenterY;
                    System.Drawing.PointF Pnt = new System.Drawing.PointF();
                    Pnt.X = (float)X;
                    Pnt.Y = (float)Y;
                    Corners.Add(Pnt);
                }
                g.DrawPolygon(LimePen, Corners.ToArray());
            }
            g.Dispose();
            LimePen.Dispose();
        }

        // =========================================================
        private int FindClosestRectangle(List<Shapes.Rectangle> Rectangles)
        {
            if (Rectangles.Count == 0)
            {
                return 0;
            }
            int closest = 0;
            float X = (Rectangles[0].Center.X - FrameCenterX);
            float Y = (Rectangles[0].Center.Y - FrameCenterY);
            float dist = X * X + Y * Y;  // we are interested only which one is closest, don't neet to take square roots to get distance right
            float dX, dY;
            for (int i = 0; i < Rectangles.Count; i++)
            {
                dX = Rectangles[i].Center.X - FrameCenterX;
                dY = Rectangles[i].Center.Y - FrameCenterY;
                if ((dX * dX + dY * dY) < dist)
                {
                    dist = dX * dX + dY * dY;
                    closest = i;
                }
            }
            return closest;
        }

        // =========================================================
        public int GetClosestRectangle(out double X, out double Y, double MaxDistance)
        // Sets X, Y position of the closest circle to the frame center in pixels, return value is number of circles found
        {
            List<Shapes.Rectangle> Rectangles = GetMeasurementRectangles(MaxDistance);
            X = 0.0;
            Y = 0.0;
            if (Rectangles.Count == 0)
            {
                return (0);
            }
            // Find the closest
            int closest = FindClosestRectangle(Rectangles);
            double zoom = GetMeasurementZoom();
            X = (Rectangles[closest].Center.X - FrameCenterX);
            Y = (Rectangles[closest].Center.Y - FrameCenterY);
            X = X / zoom;
            Y = Y / zoom;
            return (Rectangles.Count);
        }

        public List<Shapes.Rectangle> GetMeasurementRectangles(double MaxDistance)
        {
            // returns a list of circles for measurements, filters for distance (which we always do) and size, if needed

            List<Shapes.Rectangle> GoodRectangles = new List<Shapes.Rectangle>();
            Bitmap image = GetMeasurementFrame();
            if (image==null)
            {
                return (GoodRectangles);
            }
            List<Shapes.Rectangle> Rectangles = FindRectanglesFunct(image);
            image.Dispose();

            double X = 0.0;
            double Y = 0.0;
            MaxDistance = MaxDistance * GetMeasurementZoom();
            // Remove those that are more than MaxDistance away from frame center
            foreach (Shapes.Rectangle Rectangle in Rectangles)
            {
                X = (Rectangle.Center.X - FrameCenterX);
                Y = (Rectangle.Center.Y - FrameCenterY);
                if ((X * X + Y * Y) <= (MaxDistance * MaxDistance))
                {
                    GoodRectangles.Add(Rectangle);
                }
            }
            return (GoodRectangles);
        }



        // ==========================================================================================================

        private Bitmap MirrorFunct(ref Bitmap frame)
        {
            Mirror Mfilter = new Mirror(false, true);
            // apply the MirrFilter
            Mfilter.ApplyInPlace(frame);
            return (frame);
        }


        // =========================================================
        private Bitmap TestAlgorithmFunct(Bitmap frame)
        {
            frame = Grayscale.CommonAlgorithms.RMY.Apply(frame);
            Invert filter = new Invert();
            filter.ApplyInPlace(frame);
            return (frame);
        }

        // =========================================================
        private void ZoomFunct(ref Bitmap frame, double Factor)
        {
            if (Factor < 0.1)
            {
                return;
            }
            int centerX = frame.Width / 2;
            int centerY = frame.Height / 2;
            int OrgSizeX = frame.Width;
            int OrgSizeY = frame.Height;

            int fromX = centerX - (int)(centerX / Factor);
            int fromY = centerY - (int)(centerY / Factor);
            int SizeX = (int)(OrgSizeX / Factor);
            int SizeY = (int)(OrgSizeY / Factor);
            Crop CrFilter = new Crop(new Rectangle(fromX, fromY, SizeX, SizeY));
            frame = CrFilter.Apply(frame);
            ResizeBilinear RBfilter = new ResizeBilinear(OrgSizeX, OrgSizeY);
            frame = RBfilter.Apply(frame);
        }

        // =========================================================
        private void RotateByFrameCenter(int x, int y, out int px, out int py)
        {
            double theta = boxRotationRad;
            // If you rotate point (px, py) around point (ox, oy) by angle theta you'll get:
            // p'x = cos(theta) * (px-ox) - sin(theta) * (py-oy) + ox
            // p'y = sin(theta) * (px-ox) + cos(theta) * (py-oy) + oy
            px = (int)(Math.Cos(theta) * (x - FrameCenterX) - Math.Sin(theta) * (y - FrameCenterY) + FrameCenterX);
            py = (int)(Math.Sin(theta) * (x - FrameCenterX) + Math.Cos(theta) * (y - FrameCenterY) + FrameCenterY);
        }

        private void DrawGridFunct(ref Bitmap img)
        {
            Pen RedPen = new Pen(Color.Red, 1);
            Pen GreenPen = new Pen(Color.Green, 1);
            Pen BluePen = new Pen(Color.Blue, 1);
            Graphics g = Graphics.FromImage(img);
            int x1, x2, y1, y2;
            int step = 40;
            // vertical
            int i = 0;
            while (i < FrameSizeX)
            {
                // FrameCenterX, 0 to FrameCenterX, FrameSizeY
                RotateByFrameCenter(FrameCenterX + i, 0, out x1, out y1);
                RotateByFrameCenter(FrameCenterX + i, FrameSizeY, out x2, out y2);
                g.DrawLine(RedPen, x1, y1, x2, y2);
                RotateByFrameCenter(FrameCenterX - i, 0, out x1, out y1);
                RotateByFrameCenter(FrameCenterX - i, FrameSizeY, out x2, out y2);
                g.DrawLine(RedPen, x1, y1, x2, y2);
                i = i + step;
                RotateByFrameCenter(FrameCenterX + i, 0, out x1, out y1);
                RotateByFrameCenter(FrameCenterX + i, FrameSizeY, out x2, out y2);
                g.DrawLine(GreenPen, x1, y1, x2, y2);
                RotateByFrameCenter(FrameCenterX - i, 0, out x1, out y1);
                RotateByFrameCenter(FrameCenterX - i, FrameSizeY, out x2, out y2);
                g.DrawLine(GreenPen, x1, y1, x2, y2);
                i = i + step;
                RotateByFrameCenter(FrameCenterX + i, 0, out x1, out y1);
                RotateByFrameCenter(FrameCenterX + i, FrameSizeY, out x2, out y2);
                g.DrawLine(BluePen, x1, y1, x2, y2);
                RotateByFrameCenter(FrameCenterX - i, 0, out x1, out y1);
                RotateByFrameCenter(FrameCenterX - i, FrameSizeY, out x2, out y2);
                g.DrawLine(BluePen, x1, y1, x2, y2);
                i = i + step;
            }
            // horizontal
            i = 0;
            while (i < FrameSizeY)
            {
                // 0, FrameCenterY to FrameSizeX, FrameCenterY
                RotateByFrameCenter(0, FrameCenterY + i, out x1, out y1);
                RotateByFrameCenter(FrameSizeX, FrameCenterY + i, out x2, out y2);
                g.DrawLine(RedPen, x1, y1, x2, y2);
                RotateByFrameCenter(0, FrameCenterY - i, out x1, out y1);
                RotateByFrameCenter(FrameSizeX, FrameCenterY - i, out x2, out y2);
                g.DrawLine(RedPen, x1, y1, x2, y2);
                i = i + step;
                RotateByFrameCenter(0, FrameCenterY + i, out x1, out y1);
                RotateByFrameCenter(FrameSizeX, FrameCenterY + i, out x2, out y2);
                g.DrawLine(GreenPen, x1, y1, x2, y2);
                RotateByFrameCenter(0, FrameCenterY - i, out x1, out y1);
                RotateByFrameCenter(FrameSizeX, FrameCenterY - i, out x2, out y2);
                g.DrawLine(GreenPen, x1, y1, x2, y2);
                i = i + step;
                RotateByFrameCenter(0, FrameCenterY + i, out x1, out y1);
                RotateByFrameCenter(FrameSizeX, FrameCenterY + i, out x2, out y2);
                g.DrawLine(BluePen, x1, y1, x2, y2);
                RotateByFrameCenter(0, FrameCenterY - i, out x1, out y1);
                RotateByFrameCenter(FrameSizeX, FrameCenterY - i, out x2, out y2);
                g.DrawLine(BluePen, x1, y1, x2, y2);
                i = i + step;
            }

            RedPen.Dispose();
            GreenPen.Dispose();
            BluePen.Dispose();
            g.Dispose();
        }

        // =========================================================
        private void DrawDashedCrossFunct(ref Bitmap img)
        {
            Pen pen = new Pen(Color.SlateGray, 1);
            Graphics g = Graphics.FromImage(img);
            int step = FrameSizeY / 40;
            int i = step / 2;
            while (i < FrameSizeY)
            {
                g.DrawLine(pen, FrameCenterX, i, FrameCenterX, i + step);
                i = i + 2 * step;
            }
            step = FrameSizeX / 40;
            i = step / 2;
            while (i < FrameSizeX)
            {
                g.DrawLine(pen, i, FrameCenterY, i + step, FrameCenterY);
                i = i + 2 * step;
            }
            pen.Dispose();
            g.Dispose();
        }
        // =========================================================

        private void DrawCrossFunct(ref Bitmap img)
        {
            Pen pen = new Pen(Color.Red, 1);
            Graphics g = Graphics.FromImage(img);
            g.DrawLine(pen, FrameCenterX, 0, FrameCenterX, FrameSizeY);
            g.DrawLine(pen, 0, FrameCenterY, FrameSizeX, FrameCenterY);
            pen.Dispose();
            g.Dispose();
        }

        // =========================================================

        private void DrawSidemarksFunct(ref Bitmap img)
        {/*
            // default values used when show pixels is off: 
            // Draw from frame edges inwards, using ticksize that gets zoomed down
            int TickSize = (FrameSizeX / 640) * 8;
            int XstartUp = FrameSizeY;  // values used when drawing along X axis
            int XstartBot = 0;
            int YstartLeft = 0;         // values used when drawing along Y axis
            int YstartRight = FrameSizeX;
            int Xinc = Convert.ToInt32(YstartRight / SideMarksX);    // sidemarks: 10cm on machine
            int Yinc = Convert.ToInt32(XstartUp / SideMarksY);

            if (ImageBox.SizeMode == PictureBoxSizeMode.CenterImage)
            {
                // Show pixels is on, draw to middle of the image
                TickSize = 8;
                XstartUp = (FrameSizeY / 2) + 240;
                XstartBot = (FrameSizeY / 2) - 240;
                YstartLeft = (FrameSizeX / 2) - 320;
                YstartRight = (FrameSizeX / 2) + 320;
                Xinc = Convert.ToInt32(640 / SideMarksX);
                Yinc = Convert.ToInt32(480 / SideMarksY);
            }

            Pen pen = new Pen(Color.Red, 2);
            Graphics g = Graphics.FromImage(img);
            int X = YstartLeft + Xinc;
            while (X < YstartRight)
            {
                g.DrawLine(pen, X, XstartUp, X, XstartUp - TickSize);
                g.DrawLine(pen, X, XstartBot, X, XstartBot + TickSize);
                X += Xinc;
            }

            int Y = XstartBot + Yinc;
            while (Y < XstartUp)
            {
                g.DrawLine(pen, YstartLeft, Y, YstartLeft + TickSize, Y);
                g.DrawLine(pen, YstartRight, Y, YstartRight - TickSize, Y);
                Y += Yinc;
            }
*/
            Graphics g = Graphics.FromImage(img);
            int PenSize = 2;
            int tick = 12;
            int PicSizeX = FrameSizeX;
            int PicSizeY = FrameSizeY;
            if (ImageBox.SizeMode == PictureBoxSizeMode.CenterImage)
            {
                // UI shows 640x480 from middle of image. Draw tics accordingly
                PicSizeX = 640;
                PicSizeY = 480;
                PenSize = 1;
                tick = 6;
            }

            Pen pen = new Pen(Color.Red, PenSize);
            int XStart = (FrameSizeX / 2) - (PicSizeX / 2);
            int XEnd = (FrameSizeX / 2) + (PicSizeX / 2);
            int YStart = (FrameSizeY / 2) - (PicSizeY / 2);
            int YEnd = (FrameSizeY / 2) + (PicSizeY / 2);

            int Xinc = Convert.ToInt32(PicSizeX / SideMarksX);
            int X = XStart;
            while (X < XEnd)
            {
                g.DrawLine(pen, X, YEnd, X, YEnd - tick);
                g.DrawLine(pen, X, YStart, X, YStart + tick);
                X += Xinc;
            }
            int Yinc = Convert.ToInt32(PicSizeY / SideMarksY);
            int Y = YEnd;
            while (Y > YStart)
            {
                g.DrawLine(pen, XEnd, Y, XEnd - tick, Y);
                g.DrawLine(pen, XStart, Y, XStart + tick, Y);
                Y -= Yinc;
            }
            pen.Dispose();
            g.Dispose();

            pen.Dispose();
            g.Dispose();
        }

        // =========================================================
        private void DrawBoxFunct(ref Bitmap img)
        {
            Pen pen = new Pen(Color.Red, 1);
            Graphics g = Graphics.FromImage(img);
            g.DrawLine(pen, BoxPoints[0].X + FrameCenterX, BoxPoints[0].Y + FrameCenterY,
                BoxPoints[1].X + FrameCenterX, BoxPoints[1].Y + FrameCenterY);

            g.DrawLine(pen, BoxPoints[1].X + FrameCenterX, BoxPoints[1].Y + FrameCenterY,
                BoxPoints[2].X + FrameCenterX, BoxPoints[2].Y + FrameCenterY);

            g.DrawLine(pen, BoxPoints[2].X + FrameCenterX, BoxPoints[2].Y + FrameCenterY,
                BoxPoints[3].X + FrameCenterX, BoxPoints[3].Y + FrameCenterY);

            g.DrawLine(pen, BoxPoints[3].X + FrameCenterX, BoxPoints[3].Y + FrameCenterY,
                BoxPoints[0].X + FrameCenterX, BoxPoints[0].Y + FrameCenterY);
            pen.Dispose();
            g.Dispose();
        }

        private void DrawArrowFunct(ref Bitmap img, int X, int Y, int length)
        {
            Pen pen = new Pen(Color.Blue, 3);
            Graphics g = Graphics.FromImage(img);
            double angle1 = (Math.PI / -180.0) * (ArrowAngle + 90); // to radians, -180 to get ccw, +90 to start from up
            double angle2 = (Math.PI / -180.0) * (ArrowAngle - 90); // to radians, -180 to get ccw, -90 to draw from center away
            //Draw end
            g.DrawLine(pen, X, Y, (int)(X + Math.Cos(angle2) * (double)length), (int)(Y + Math.Sin(angle2) * (double)length));
            // draw head
            System.Drawing.Drawing2D.AdjustableArrowCap bigArrow = new System.Drawing.Drawing2D.AdjustableArrowCap(6, 6);
            pen.CustomEndCap = bigArrow;
            g.DrawLine(pen, X, Y, (int)(X + Math.Cos(angle1) * (double)length), (int)(Y + Math.Sin(angle1) * (double)length));
            pen.Dispose();
            g.Dispose();
        }


        // =========================================================
        // Snapshot handling
        // =========================================================

        // repeated rotations destroy the image. We'll store the original here and rotate only once.
        public Bitmap SnapshotOriginalImage = new Bitmap(640, 480);

        // This is the image drawn by draw snapshot function (both public so they can be set externally).
        public Bitmap SnapshotImage = new Bitmap(640, 480);

        public double SnapshotRotation = 0.0;  // rotation when snapshot was taken

        public Color SnapshotColor { get; set; }

        public void TakeSnapshot()
        {
            Bitmap image = GetMeasurementFrame();
            if (image==null)
            {
                MainForm.DisplayText("Could not get a snapshot image", KnownColor.DarkRed, true);
                return;
            }

            Color peek;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    peek = image.GetPixel(x, y);
                    if (peek.R != 0)  // i.e. background
                    {
                        image.SetPixel(x, y, SnapshotColor);
                    }
                }
            }

            image.MakeTransparent(Color.Black);
            SnapshotImage = new Bitmap(image);
            SnapshotOriginalImage = new Bitmap(image);
            image.Dispose();
        }



        // =========================================================
        public bool rotating = false;
        private bool overlaying = false;

        private Bitmap Draw_SnapshotFunct(Bitmap image)
        {
            if (rotating)
            {
                return (image);
            }
            overlaying = true;
            Graphics g = Graphics.FromImage(image);
            g.DrawImage(SnapshotImage, new System.Drawing.Point(0, 0));
            g.Dispose();
            overlaying = false;
            return (image);
        }

        // =========================================================
        public void RotateSnapshot(double deg)
        {
            while (overlaying)
            {
                Thread.Sleep(10);
            }
            rotating = true;
            // Convert to 24 bpp RGB Image
            Rectangle dimensions = new Rectangle(0, 0, SnapshotOriginalImage.Width, SnapshotOriginalImage.Height);
            Bitmap Snapshot24b = new Bitmap(SnapshotOriginalImage.Width, SnapshotOriginalImage.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (Graphics gr = Graphics.FromImage(Snapshot24b))
            {
                gr.DrawImage(SnapshotOriginalImage, dimensions);
                gr.Dispose();
            }

            RotateNearestNeighbor filter = new RotateNearestNeighbor(deg - SnapshotRotation, true);
            Snapshot24b = filter.Apply(Snapshot24b);
            // convert back to 32b, to have transparency
            Snapshot24b.MakeTransparent(Color.Black);
            SnapshotImage = Snapshot24b;
            rotating = false;
        }


        // =========================================================
        // Measurements on video images
        // =========================================================
        #region Measurements

        // Caller = any function doing measurement from video frames. 
        // The list of functions processing the image used in measurements, set by caller:
        List<AForgeFunction> MeasurementFunctions = new List<AForgeFunction>();

        // Measurement parameters: min and max size, max distance from initial location, set by caller:
        public MeasurementParametersClass MeasurementParameters = new MeasurementParametersClass();

        // ==========================================================================================================
        // Measurements are done by taking one frame and processing that:
        private bool CopyFrame = false;     // Tells we need to take a frame from the stream 
        private Bitmap TemporaryFrame;      // The frame is stored here.

        // The caller builds the MeasurementFunctions list:

        public void BuildMeasurementFunctionsList(List<AForgeFunctionDefinition> UiList)
        {
            JoggingRequested = false;     // BuildFunctionsList() sets this to true, if manual jog is needed
            MeasurementFunctions = BuildFunctionsList(UiList);
        }

        // And calls xx_measure() funtion. 
        // The xxx_measure funtion calls GetMeasurementFrame() function, that takes a frame from the stream, 
        // processes it with the MeasurementFunctions list and returns the processed frame.


        private Bitmap GetMeasurementFrame()
        {
            if (JoggingRequested)
            {
                ManualJogFunc();
            }
            // Take a snapshot:
            CopyFrame = true;
            int tries = 100;
            while (tries > 0)
            {
                tries--;
                if (!CopyFrame)
                {
                    break;
                }
                Thread.Sleep(10);
                Application.DoEvents();
            }
            if (CopyFrame)
            {
                // failed!
                MainForm.DisplayText("*** GetMeasurementFrame() failed!", KnownColor.Purple);
                return TemporaryFrame;
            }

            if (MeasurementFunctions != null)
            {
                foreach (AForgeFunction f in MeasurementFunctions)
                {
                    f.func(ref TemporaryFrame, f.parameter_int, f.parameter_double, f.R, f.G, f.B,
                        f.parameter_doubleA, f.parameter_doubleB, f.parameter_doubleC);
                }
            }
            return TemporaryFrame;
        }

        // Since the measured image might be zoomed in, we need the value, so that we can convert to real measurements (public for debug)
        public double GetMeasurementZoom()
        {
            double zoom = 1.0;
            foreach (AForgeFunction f in MeasurementFunctions)
            {
                if (f.func == Meas_ZoomFunc)
                {
                    zoom = zoom * f.parameter_double;
                }
            }
            return zoom;
        }

        // UI also needs the zoom factor from the DisplayFunctions
        public double GetDisplayZoom()
        {
            double zoom = 1.0;
            foreach (AForgeFunction f in DisplayFunctions)
            {
                if (f.func == Meas_ZoomFunc)
                {
                    zoom = zoom * f.parameter_double;
                }
            }
            return zoom;
        }

        // ==========================================================================================================
        // Measure
        // ==========================================================================================================
        public double XmmPerPixel;
        public double YmmPerPixel;

        private void DisplayShapes(List<Shapes.Shape> Shapes, int StartFrom, double XmmPpix, double YmmPpix)
        {
            if ((Shapes.Count - StartFrom) == 0)
            {
                MainForm.DisplayText("    No results.");
                return;
            }
            string OutString = "";
            string Xpxls;
            string Ypxls;
            string Xmms;
            string Ymms;
            string Xsize;
            string Ysize;
            string SizeXmm;
            string SizeYmm;
            string A;

            for (int i = StartFrom; i < Shapes.Count; i++)
            {
                Xpxls = String.Format("{0,6:0.0}", Shapes[i].Center.X - FrameCenterX);
                Ypxls = String.Format("{0,6:0.0}", FrameCenterY - Shapes[i].Center.Y);
                Xmms = String.Format("{0,7:0.000}", (Shapes[i].Center.X - FrameCenterX) * XmmPpix);
                Ymms = String.Format("{0,7:0.000}", (FrameCenterY - Shapes[i].Center.Y) * YmmPpix);
                Xsize = String.Format("{0,5:0.0}", Shapes[i].Xsize);
                Ysize = String.Format("{0,5:0.0}", Shapes[i].Ysize);
                SizeXmm = String.Format("{0,4:0.00}", Shapes[i].Xsize * XmmPpix);
                SizeYmm = String.Format("{0,4:0.00}", Shapes[i].Ysize * YmmPpix);
                A= String.Format("{0,5:0.00}", Shapes[i].Angle);
                OutString = "p: " + Xpxls + ", " + Ypxls + "px; " + Xmms + ", " + Ymms + "mm; " +
                    "s: " + Xsize + ", " + Ysize + "px; " + SizeXmm + ", " + SizeYmm + "mm; A: " + A;
                MainForm.DisplayText(OutString);
            }
        }


        // =========================================================
        private double DistanceToCenter(Shapes.Shape shape)
        {
            double X = shape.Center.X - FrameCenterX;
            double Y = FrameCenterY - shape.Center.Y;

            return Math.Sqrt(X * X + Y * Y);
        }

        public bool Measure(out double Xresult, out double Yresult, out double Aresult, bool DisplayResults = false )
        {
            Xresult = 0.0;
            Yresult = 0.0;
            Aresult = 0.0;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            if ((!MeasurementParameters.SearchRounds) && (!MeasurementParameters.SearchRectangles)
                && (!MeasurementParameters.SearchComponentOutlines) && (!MeasurementParameters.SearchComponentPads))
            {
                MainForm.DisplayText("Nothing to search for. Check some of the \"Features to search for\" boxes.", KnownColor.Red, true);
                return false;
            }

            if ((MeasurementParameters.XUniqueDistance < 0.01) ||
                (MeasurementParameters.YUniqueDistance < 0.01))
            {
                MainForm.DisplayText("Discard distance is 0 or less.", KnownColor.Red, true);
                return false;
            }

            Bitmap image = GetMeasurementFrame();
            if (image==null)
            {
                MainForm.DisplayText("Could not get a snapshot image", KnownColor.DarkRed, true);
                return false;
            }

            // Find candidates:
            stopwatch.Stop();   // Don't time diagnostic messages
            if (DisplayResults)
            {
                MainForm.DisplayText("Result candidates:");
            }
            stopwatch.Start();

            List<Shapes.Shape> Candidates = new List<Shapes.Shape>();
            double zoom = GetMeasurementZoom();
            double XmmPpix = XmmPerPixel / zoom;
            double YmmPpix = YmmPerPixel / zoom;

            if (MeasurementParameters.SearchRounds)
            {
                List<Shapes.Circle> Circles = FindCirclesFunct(image);
                foreach (Shapes.Circle circle in Circles)
                {
                    Candidates.Add(new Shapes.Shape()
                    {
                        Center = circle.Center,
                        Angle = circle.Angle,
                        Xsize = circle.Radius * 2.0,
                        Ysize = circle.Radius * 2.0
                    });
                }
                stopwatch.Stop();
                if (DisplayResults)
                {
                    MainForm.DisplayText("Circles:");
                    DisplayShapes(Candidates, 0, XmmPpix, YmmPpix);
                }
                stopwatch.Start();
            }
            int count = Candidates.Count;

            if (MeasurementParameters.SearchRectangles)
            {
                List<Shapes.Rectangle> Regtangles = FindRectanglesFunct(image);
                foreach (Shapes.Rectangle regt in Regtangles)
                {
                    Candidates.Add(new Shapes.Shape()
                    {
                        Center = regt.Center,
                        Angle = regt.Angle,
                        Xsize = regt.Xsize,
                        Ysize = regt.Ysize
                    });
                }
                stopwatch.Stop();
                if (DisplayResults)
                {
                    MainForm.DisplayText("Regtangles:");
                    DisplayShapes(Candidates, count, XmmPpix, YmmPpix);
                }
                stopwatch.Start();
            }
            count = Candidates.Count;

            if (MeasurementParameters.SearchComponentOutlines)
            {
                List<Shapes.Component> Components = FindComponentsFromOutline_Funct(image);
                foreach (Shapes.Component comp in Components)
                {
                    Candidates.Add(new Shapes.Shape()
                    {
                        Center = comp.Center,
                        Angle = comp.Angle,
                        Xsize = comp.Xsize,
                        Ysize = comp.Ysize
                    });
                }

                stopwatch.Stop();
                if (DisplayResults)
                {
                    MainForm.DisplayText("Components from outlines:");
                    DisplayShapes(Candidates, count, XmmPpix, YmmPpix);
                }
                stopwatch.Start();
            }
            count = Candidates.Count;

            if (MeasurementParameters.SearchComponentPads)
            {
                List<Shapes.Component> Components = FindComponentsFromPads_Funct(image);
                foreach (Shapes.Component comp in Components)
                {
                    Candidates.Add(new Shapes.Shape()
                    {
                        Center = comp.Center,
                        Angle = comp.Angle,
                        Xsize = comp.Xsize,
                        Ysize = comp.Ysize
                    });
                }

                stopwatch.Stop();
                if (DisplayResults)
                {
                    MainForm.DisplayText("Components from pads:");
                    DisplayShapes(Candidates, count, XmmPpix, YmmPpix);
                }
                stopwatch.Start();
            }

            // Filter for size
            List<Shapes.Shape> FilteredForSize = new List<Shapes.Shape>();
            foreach (Shapes.Shape shape in Candidates)
            {
                if (  ((shape.Xsize * XmmPpix) > MeasurementParameters.Xmin) &&
                      ((shape.Xsize * XmmPpix) < MeasurementParameters.Xmax) &&
                      ((shape.Ysize * YmmPpix) > MeasurementParameters.Ymin) &&
                      ((shape.Ysize * YmmPpix) < MeasurementParameters.Ymax))

                {
                    FilteredForSize.Add(shape);
                }
            }
            stopwatch.Stop();
            if (DisplayResults)
            {
                MainForm.DisplayText("Filtered for size, results:");
            }
            stopwatch.Start();

            if (FilteredForSize.Count == 0)
            {
                if (DisplayResults)
                {
                    MainForm.DisplayText("No items left.");
                    MainForm.DisplayText("Elapsed time " + stopwatch.ElapsedMilliseconds.ToString() + "ms");
                }
                else
                {
                    MainForm.DisplayText("Camera Measure(), no items left after size filtering.", KnownColor.Red, true);
                }
                return false;
            }

            stopwatch.Stop();
            if (DisplayResults)
            {
                DisplayShapes(FilteredForSize, 0, XmmPpix, YmmPpix);
            };
            stopwatch.Start();

            // Filter for distance
            List<Shapes.Shape> FilteredForDistance = new List<Shapes.Shape>();
            foreach (Shapes.Shape shape in FilteredForSize)
            {
                double Xdist = Math.Abs((shape.Center.X - FrameCenterX) * XmmPpix);
                double Ydist = Math.Abs((FrameCenterY - shape.Center.Y) * YmmPpix);

                if ((Xdist< MeasurementParameters.XUniqueDistance) && (Ydist < MeasurementParameters.YUniqueDistance))
                {
                    FilteredForDistance.Add(shape);
                }
            }
            stopwatch.Stop();

            if (FilteredForDistance.Count == 0)
            {
                if (DisplayResults)
                {
                    MainForm.DisplayText("Filtered for distance, no items left.");
                    MainForm.DisplayText("Elapsed time " + stopwatch.ElapsedMilliseconds.ToString() + "ms");
                }
                else
                {
                    MainForm.DisplayText("Camera Measure(), no items left after distance filtering.", KnownColor.Red, true);
                }
                return false;
            }

            Xresult = (FilteredForDistance[0].Center.X - FrameCenterX) * XmmPpix;
            Yresult = (FrameCenterY - FilteredForDistance[0].Center.Y) * YmmPpix;
            Aresult = FilteredForDistance[0].Angle;

            if (!DisplayResults)
            {
                if (FilteredForDistance.Count != 1)
                {
                    MainForm.DisplayText("Camera Measure(): result is not unique", KnownColor.Red, true);
                    MainForm.DisplayText("Elapsed time " + stopwatch.ElapsedMilliseconds.ToString() + "ms");
                    return false;
                }
                else
                {
                    return true;
                }
            }

            MainForm.DisplayText("Filtered for distance, results:");
            DisplayShapes(FilteredForDistance, 0, XmmPpix, YmmPpix);
            if (FilteredForDistance.Count != 1)
            {
                MainForm.DisplayText("Result is NOT unique!", KnownColor.Red, true);
                MainForm.DisplayText("Elapsed time " + stopwatch.ElapsedMilliseconds.ToString() + "ms");
                return false;
            }
            MainForm.DisplayText( "Result: X= " + Xresult.ToString("0.000", CultureInfo.InvariantCulture) +
                                        ", Y= " + Yresult.ToString("0.000", CultureInfo.InvariantCulture) +
                                        ", A= " + Aresult.ToString("0.00", CultureInfo.InvariantCulture));
            MainForm.DisplayText("Elapsed time " + stopwatch.ElapsedMilliseconds.ToString() + "ms");
            return true;
        }

        #endregion
    }
}
