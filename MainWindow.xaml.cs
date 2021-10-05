//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;
    using Microsoft.Kinect;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region CONSTANTS
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RENDER_WIDTH = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RENDER_HEIGHT = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JOINT_THICKNESS = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BODY_CENTER_THICKNESS = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double CLIP_BOUNDS_THICKNESS = 10;
        #endregion

        #region SKELETON DRAWING PROPERTIES
        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush _centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush _trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush _inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen _trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen _inferredBonePen = new Pen(Brushes.Gray, 1);
        #endregion

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor _sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup _drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage _skeletonImageSource;

        #region COLOR_PROPERTIES

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap _colorBitmap;

        /// <summary>
        /// Intermediate storage for the color data received from the camera
        /// </summary>
        private byte[] _colorPixels;
        #endregion

        #region DEPTH PROPERTIES
        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap _depthColorBitmap;

        /// <summary>
        /// Intermediate storage for the depth data received from the camera
        /// </summary>
        private DepthImagePixel[] _depthPixels;

        /// <summary>
        /// Intermediate storage for the depth data converted to color
        /// </summary>
        private byte[] _depthColorPixels;
        #endregion

        private bool _isRecording = false;

        #region REPLAY SAVED JSON DATA
        private DispatcherTimer _dispatcherTimer = new DispatcherTimer();
        //Current file index to draw
        private int _currentJsonIdx = 0;
        private FileInfo[] _jsonFiles;
        private bool _isLoadingJsonData = false;
        #endregion

        private string _skeletonDir = Path.Combine(Directory.GetCurrentDirectory(), "skeleton");
        private string _colorDir = Path.Combine(Directory.GetCurrentDirectory(), "color");
        private string _depthDir = Path.Combine(Directory.GetCurrentDirectory(), "depth");

        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings()
        {
            Converters = new List<JsonConverter> { new StringEnumConverter() }
        };

        public string GestureName { get; set; } = "gesture1";



        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            _drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            _skeletonImageSource = new DrawingImage(_drawingGroup);

            // Display the drawing using our image control
            SkeltonImage.Source = _skeletonImageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    _sensor = potentialSensor;
                    break;
                }
            }

            if (null != _sensor)
            {
                #region SKELETON SETTINGS
                // Turn on the skeleton stream to receive skeleton frames
                _sensor.SkeletonStream.Enable();
                // Add an event handler to be called whenever there is new color frame data
                _sensor.SkeletonFrameReady += OnFrameReadySenesorSkeleton;
                #endregion

                #region COLOR SETTINGS
                // Turn on the color stream to receive color frames
                _sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                // Allocate space to put the pixels we'll receive
                _colorPixels = new byte[_sensor.ColorStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                _colorBitmap = new WriteableBitmap(_sensor.ColorStream.FrameWidth, _sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                ColorImage.Source = _colorBitmap;
                // Add an event handler to be called whenever there is new color frame data
                _sensor.ColorFrameReady += OnFrameReadySensorColor;
                #endregion

                #region DEPTH SETTINGS
                // Turn on the depth stream to receive depth frames
                _sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);

                // Allocate space to put the depth pixels we'll receive
                _depthPixels = new DepthImagePixel[_sensor.DepthStream.FramePixelDataLength];

                // Allocate space to put the color pixels we'll create
                _depthColorPixels = new byte[_sensor.DepthStream.FramePixelDataLength * sizeof(int)];

                // This is the bitmap we'll display on-screen
                _depthColorBitmap = new WriteableBitmap(_sensor.DepthStream.FrameWidth, _sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                DepthImage.Source = _depthColorBitmap;

                // Add an event handler to be called whenever there is new depth frame data
                _sensor.DepthFrameReady += OnFrameReadySensorDepthFrame;
                #endregion

                _sensor.AllFramesReady += OnAllFramesReady;
                // Start the sensor!
                try
                {
                    _sensor.Start();
                }
                catch (IOException)
                {
                    _sensor = null;
                }
            }

            if (null == _sensor)
            {
                statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        private void OnAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            if (_isRecording)
            {
                SaveAllImages();
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != _sensor)
            {
                _sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's ColorFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void OnFrameReadySensorColor(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(_colorPixels);

                    // Write the pixel data into our bitmap
                    _colorBitmap.WritePixels(
                        new Int32Rect(0, 0, _colorBitmap.PixelWidth, _colorBitmap.PixelHeight),
                        _colorPixels,
                        _colorBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }
        }

        private void OnFrameReadySensorDepthFrame(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(_depthPixels);

                    // Get the min and max reliable depth for the current frame
                    int minDepth = depthFrame.MinDepth;
                    int maxDepth = depthFrame.MaxDepth;

                    // Convert the depth to RGB
                    int colorPixelIndex = 0;
                    for (int i = 0; i < _depthPixels.Length; ++i)
                    {
                        // Get the depth for this pixel
                        short depth = _depthPixels[i].Depth;

                        // To convert to a byte, we're discarding the most-significant
                        // rather than least-significant bits.
                        // We're preserving detail, although the intensity will "wrap."
                        // Values outside the reliable depth range are mapped to 0 (black).

                        // Note: Using conditionals in this loop could degrade performance.
                        // Consider using a lookup table instead when writing production code.
                        // See the KinectDepthViewer class used by the KinectExplorer sample
                        // for a lookup table example.
                        byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 0);

                        // Write out blue byte
                        _depthColorPixels[colorPixelIndex++] = intensity;

                        // Write out green byte
                        _depthColorPixels[colorPixelIndex++] = intensity;

                        // Write out red byte                        
                        _depthColorPixels[colorPixelIndex++] = intensity;

                        // We're outputting BGR, the last byte in the 32 bits is unused so skip it
                        // If we were outputting BGRA, we would write alpha here.
                        ++colorPixelIndex;
                    }

                    // Write the pixel data into our bitmap
                    _depthColorBitmap.WritePixels(
                        new Int32Rect(0, 0, _depthColorBitmap.PixelWidth, _depthColorBitmap.PixelHeight),
                        _depthColorPixels,
                        _depthColorBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void OnFrameReadySenesorSkeleton(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);

                    if (_isRecording)
                    {
                        string time = DateTime.Now.ToString("hh'-'mm'-'ss.fff", CultureInfo.CurrentUICulture.DateTimeFormat);

                        string jsonPath = Path.Combine(_skeletonDir, "Skeleton-" + time + ".json");
                        string jsonData = JsonConvert.SerializeObject(skeletons, Formatting.Indented, new StringEnumConverter());

                        try
                        {
                            File.WriteAllText(jsonPath, jsonData);
                        }
                        catch (IOException)
                        {
                            statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteFailed, jsonPath);
                        }
                    }
                }
            }

            if (_isLoadingJsonData == false)
            {
                DrawSkeletons(skeletons);
            }
        }

        #region REPLAY JSON SKELETON DATA

        private void OnDrawSkeletonFromJsonClick(object sender, RoutedEventArgs e)
        {
            var skeletonDirInfo = new DirectoryInfo(_skeletonDir);
            _jsonFiles = skeletonDirInfo.GetFiles("*.json");

            _currentJsonIdx = 0;
            _dispatcherTimer.Tick += DrawSkeletonFrameFromJson;
            _dispatcherTimer.Interval = TimeSpan.FromMilliseconds(50);
            _dispatcherTimer.Start();
            _isLoadingJsonData = true;
        }

        private void DrawSkeletonFrameFromJson(object sender, EventArgs e)
        {
            if (_jsonFiles.Length <= _currentJsonIdx + 1)
            {
                _isLoadingJsonData = false;
                _dispatcherTimer.Stop();
                return;
            }

            var jsonFile = _jsonFiles[_currentJsonIdx++];

            var skeletons = JsonConvert.DeserializeObject<Skeleton[]>(File.ReadAllText(jsonFile.FullName), _jsonSettings);
            var skeletonsTemp = JsonConvert.DeserializeObject<SkeletonTemp[]>(File.ReadAllText(jsonFile.FullName), _jsonSettings);
            for (int i = 0; i < skeletons.Length; i++)
            {
                //num of joint types = 20
                for (int j = 0; j < 20; j++)
                {
                    var joint = skeletons[i].Joints[(JointType)j];
                    var jointTemp = skeletonsTemp[i].Joints[j];
                    joint.Position = jointTemp.Position;
                    joint.TrackingState = jointTemp.TrackingState;
                    skeletons[i].Joints[(JointType)j] = joint;
                }
            }

            DrawSkeletons(skeletons);
        }

        #endregion

        /// <summary>
        /// Handles the user clicking on the screenshot button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void ButtonScreenshotClick(object sender, RoutedEventArgs e)
        {
            SaveAllImages();
            Console.WriteLine(GestureName);
        }

        /// <summary>
        /// This is to save color images with only half framerate.
        /// </summary>
        private static bool _saveColorImage = true;

        /// <summary>
        /// Saves color and depth images to files.
        /// </summary>
        private void SaveAllImages()
        {
            if (null == _sensor)
            {
                statusBarText.Text = Properties.Resources.ConnectDeviceFirst;
                return;
            }

            string time = DateTime.Now.ToString("hh'-'mm'-'ss.fff", CultureInfo.CurrentUICulture.DateTimeFormat);

            SaveImage(_depthColorBitmap, _depthDir, "DepthSnapshot-" + time);

            if (_saveColorImage)
            {
                SaveImage(_colorBitmap, _colorDir, "ColorSnapshot-" + time);
            }

            _saveColorImage = !_saveColorImage;
        }

        private void SaveImage(WriteableBitmap bitmap, string dir, string filename)
        {
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            string path = Path.Combine(dir, filename + ".png");

            // write the new file to disk
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteSuccess, path);
            }
            catch (IOException)
            {
                statusBarText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Properties.Resources.ScreenshotWriteFailed, path);
            }
        }

        /// <summary>
        /// Handles the checking or unchecking of the recording mode
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void RecordingModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != _sensor)
            {
                _isRecording = CheckBoxRecordingMode.IsChecked.GetValueOrDefault();

                if (_isRecording)
                {
                    //Create folders
                    var gestureFolder = Path.Combine(Directory.GetCurrentDirectory(), GestureName);

                    if (Directory.Exists(gestureFolder) == false)
                    {
                        Directory.CreateDirectory(gestureFolder);
                    }

                    var time = DateTime.Now.ToString("hh'-'mm'-'ss.fff", CultureInfo.CurrentUICulture.DateTimeFormat);
                    var currentRecordDir = Path.Combine(gestureFolder, $"{GestureName}-{time}");

                    _skeletonDir = Path.Combine(currentRecordDir, "skeleton");
                    _colorDir = Path.Combine(currentRecordDir, "color");
                    _depthDir = Path.Combine(currentRecordDir, "depth");

                    CreateDataDiretories();
                }
            }
        }

        private void CreateDataDiretories()
        {
            if (Directory.Exists(_depthDir) == false)
            {
                Directory.CreateDirectory(_depthDir);
            }

            if (Directory.Exists(_colorDir) == false)
            {
                Directory.CreateDirectory(_colorDir);
            }

            if (Directory.Exists(_skeletonDir) == false)
            {
                Directory.CreateDirectory(_skeletonDir);
            }
        }

        #region SKELETON DRAWINGS
        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = _trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = _inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, SkeletonPointToScreen(joint.Position), JOINT_THICKNESS, JOINT_THICKNESS);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = _sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = _inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = _trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, SkeletonPointToScreen(joint0.Position), SkeletonPointToScreen(joint1.Position));
        }

        ///// <summary>
        ///// Handles the checking or unchecking of the seated mode combo box
        ///// </summary>
        ///// <param name="sender">object sending the event</param>
        ///// <param name="e">event arguments</param>
        //private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        //{
        //    if (null != _sensor)
        //    {
        //        if (CheckBoxSeatedMode.IsChecked.GetValueOrDefault())
        //        {
        //            _sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
        //        }
        //        else
        //        {
        //            _sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
        //        }
        //    }
        //}

        /// <summary>
        /// Draw skeletons
        /// </summary>
        /// <param name="skeletons"></param>
        private void DrawSkeletons(Skeleton[] skeletons)
        {
            using (DrawingContext dc = _drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RENDER_WIDTH, RENDER_HEIGHT));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skeleton in skeletons)
                    {
                        RenderClippedEdges(skeleton, dc);

                        if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            DrawBonesAndJoints(skeleton, dc);
                        }
                        else if (skeleton.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            _centerPointBrush,
                            null,
                            SkeletonPointToScreen(skeleton.Position),
                            BODY_CENTER_THICKNESS,
                            BODY_CENTER_THICKNESS);
                        }
                    }
                }

                // prevent drawing outside of our render area
                _drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RENDER_WIDTH, RENDER_HEIGHT));
            }
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RENDER_HEIGHT - CLIP_BOUNDS_THICKNESS, RENDER_WIDTH, CLIP_BOUNDS_THICKNESS));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RENDER_WIDTH, CLIP_BOUNDS_THICKNESS));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, CLIP_BOUNDS_THICKNESS, RENDER_HEIGHT));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RENDER_WIDTH - CLIP_BOUNDS_THICKNESS, 0, CLIP_BOUNDS_THICKNESS, RENDER_HEIGHT));
            }
        }
        #endregion
    }
}