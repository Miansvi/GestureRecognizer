using GestureRecognizer.Camera;
using System.Collections.ObjectModel;

#if IOS || MACCATALYST
using DecodeDataType = UIKit.UIImage;
#elif ANDROID
using DecodeDataType = Android.Graphics.Bitmap;
#elif WINDOWS
using DecodeDataType = Windows.Graphics.Imaging.SoftwareBitmap;
#else
using DecodeDataType = System.Object;
#endif

namespace GestureRecognizer;

public class CameraView : View, ICameraView
{
    public static readonly BindableProperty SelfProperty = BindableProperty.Create(nameof(Self), typeof(CameraView), typeof(CameraView), null, BindingMode.OneWayToSource);
    public static readonly BindableProperty FlashModeProperty = BindableProperty.Create(nameof(FlashMode), typeof(FlashMode), typeof(CameraView), FlashMode.Disabled);
    public static readonly BindableProperty TorchEnabledProperty = BindableProperty.Create(nameof(TorchEnabled), typeof(bool), typeof(CameraView), false);
    public static readonly BindableProperty CamerasProperty = BindableProperty.Create(nameof(Cameras), typeof(ObservableCollection<CameraInfo>), typeof(CameraView), new ObservableCollection<CameraInfo>());
    public static readonly BindableProperty NumCamerasDetectedProperty = BindableProperty.Create(nameof(NumCamerasDetected), typeof(int), typeof(CameraView), 0);
    public static readonly BindableProperty CameraProperty = BindableProperty.Create(nameof(Camera), typeof(CameraInfo), typeof(CameraView), null, propertyChanged: CameraChanged);
    public static readonly BindableProperty MirroredImageProperty = BindableProperty.Create(nameof(MirroredImage), typeof(bool), typeof(CameraView), false);
    public static readonly BindableProperty GestureDetectionEnabledProperty = BindableProperty.Create(nameof(GestureDetectionEnabled), typeof(bool), typeof(CameraView), false);
    public static readonly BindableProperty ZoomFactorProperty = BindableProperty.Create(nameof(ZoomFactor), typeof(float), typeof(CameraView), 1f);
    public static readonly BindableProperty AutoStartPreviewProperty = BindableProperty.Create(nameof(AutoStartPreview), typeof(bool), typeof(CameraView), false, propertyChanged: AutoStartPreviewChanged);
    
    /// <summary>
    /// Binding property for use this control in MVVM.
    /// </summary>
    public CameraView Self
    {
        get { return (CameraView)GetValue(SelfProperty); }
        set { SetValue(SelfProperty, value); }
    }
    /// <summary>
    /// Flash mode for take a photo. This is a bindable property.
    /// </summary>
    public FlashMode FlashMode
    {
        get { return (FlashMode)GetValue(FlashModeProperty); }
        set { SetValue(FlashModeProperty, value); }
    }
    /// <summary>
    /// Turns the camera torch on and off if available. This is a bindable property.
    /// </summary>
    public bool TorchEnabled
    {
        get { return (bool)GetValue(TorchEnabledProperty); }
        set { SetValue(TorchEnabledProperty, value); }
    }
    /// <summary>
    /// List of available cameras in the device. This is a bindable property.
    /// </summary>
    public ObservableCollection<CameraInfo> Cameras
    {
        get { return (ObservableCollection<CameraInfo>)GetValue(CamerasProperty); }
        set { SetValue(CamerasProperty, value); }
    }
    /// <summary>
    /// Indicates the number of available cameras in the device.
    /// </summary>
    public int NumCamerasDetected
    {
        get { return (int)GetValue(NumCamerasDetectedProperty); }
        set { SetValue(NumCamerasDetectedProperty, value); }
    }
    /// <summary>
    /// Set the camera to use by the controler. This is a bindable property.
    /// </summary>
    public CameraInfo Camera
    {
        get { return (CameraInfo)GetValue(CameraProperty); }
        set { SetValue(CameraProperty, value); }
    }
    /// <summary>
    /// Turns a mirror image of the camera on and off. This is a bindable property.
    /// </summary>
    public bool MirroredImage
    {
        get { return (bool)GetValue(MirroredImageProperty); }
        set { SetValue(MirroredImageProperty, value); }
    }
    /// <summary>
    /// Turns on and off the gesture detection. This is a bindable property.
    /// </summary>
    public bool GestureDetectionEnabled
    {
        get { return (bool)GetValue(GestureDetectionEnabledProperty); }
        set { SetValue(GestureDetectionEnabledProperty, value); }
    }
    /// <summary>
    /// Indicates the maximun number of simultaneous running threads for gesture detection
    /// </summary>
    public int GestureDetectionMaxThreads { get; set; } = 0;
    internal int currentThreads = 0;
    internal object currentThreadsLocker = new();
    /// <summary>
    /// The zoom factor for the current camera in use. This is a bindable property.
    /// </summary>
    public float ZoomFactor
    {
        get { return (float)GetValue(ZoomFactorProperty); }
        set { SetValue(ZoomFactorProperty, value); }
    }
    /// <summary>
    /// Indicates the minimum zoom factor for the camera in use. This property is refreshed when the "Camera" property change.
    /// </summary>
    public float MinZoomFactor
    {
        get
        {
            if (Camera != null)
                return Camera.MinZoomFactor;
            else
                return 1f;
        }
    }
    /// <summary>
    /// Indicates the maximum zoom factor for the camera in use. This property is refreshed when the "Camera" property change.
    /// </summary>
    public float MaxZoomFactor
    {
        get
        {
            if (Camera != null)
                return Camera.MaxZoomFactor;
            else
                return 1f;
        }
    }
    /// <summary>
    /// Starts/Stops the Preview if camera property has been set
    /// </summary>
    public bool AutoStartPreview
    {
        get { return (bool)GetValue(AutoStartPreviewProperty); }
        set { SetValue(AutoStartPreviewProperty, value); }
    }
    public delegate void GestureResultHandler(object? sender, GestureDetectionEventArgs args);
    /// <summary>
    /// Event launched every time a code is detected in the image if "BarCodeDetectionEnabled" is set to true.
    /// </summary>
    public event GestureResultHandler? GestureDetected;
    public event EventHandler? GestureProcessStarted;
    public event EventHandler? GestureProcessFinished;
    /// <summary>
    /// Event launched when "Cameras" property has been loaded.
    /// </summary>
    public event EventHandler? CamerasLoaded;
    /// <summary>
    /// A static reference to the last CameraView created.
    /// </summary>
    public static CameraView? Current { get; set; }

    internal DateTime lastSnapshot = DateTime.Now;
    internal Size PhotosResolution = new(0, 0);

    public CameraView()
    {
        HandlerChanged += CameraView_HandlerChanged;
        Current = this;
    }
    private void CameraView_HandlerChanged(object? sender, EventArgs e)
    {
        if (Handler != null)
        {
            CamerasLoaded?.Invoke(this, EventArgs.Empty);
            Self = this;
        }
    }

    internal void DecodeGesture(List<float[][][]> data)
    {
        GestureProcessStarted?.Invoke(this, EventArgs.Empty);
        var results = Model.ProcessVideo(data);
        if (results.Count > 0)
            GestureDetected?.Invoke(this, new GestureDetectionEventArgs()
            {
                Result = results
            });
        GestureProcessFinished?.Invoke(this, EventArgs.Empty);
    }
    private static void CameraChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (newValue != null && oldValue != newValue && bindable is CameraView cameraView && newValue is CameraInfo)
        {
            cameraView.OnPropertyChanged(nameof(MinZoomFactor));
            cameraView.OnPropertyChanged(nameof(MaxZoomFactor));
        }
    }
    private static async void AutoStartPreviewChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (oldValue != newValue && bindable is CameraView control)
        {
            try
            {
                if ((bool)newValue)
                    await control.StartCameraAsync();
                else
                    await control.StopCameraAsync();
            }
            catch { }

        }
    }
    /// <summary>
    /// Start playback of the selected camera async. "Camera" property must not be null.
    /// <paramref name="Resolution"/> Indicates the resolution for the preview and photos taken with TakePhotoAsync (must be in Camera.AvailableResolutions). If width or height is 0, max resolution will be taken.
    /// </summary>
    public async Task<CameraResult> StartCameraAsync(Size Resolution = default)
    {
        CameraResult result = CameraResult.AccessError;
        if (Camera != null)
        {
            PhotosResolution = Resolution;
            if (Resolution.Width != 0 && Resolution.Height != 0)
            {
                if (!Camera.AvailableResolutions.Any(r => r.Width == Resolution.Width && r.Height == Resolution.Height))
                    return CameraResult.ResolutionNotAvailable;
            }
            if (Handler != null && Handler is CameraViewHandler handler)
            {
                result = await handler.StartCameraAsync(Resolution);
                if (result == CameraResult.Success)
                {
                    OnPropertyChanged(nameof(MinZoomFactor));
                    OnPropertyChanged(nameof(MaxZoomFactor));
                }
            }
        }
        else
            result = CameraResult.NoCameraSelected;

        return result;
    }
    /// <summary>
    /// Stop playback of the selected camera async.
    /// </summary>
    public async Task<CameraResult> StopCameraAsync()
    {
        CameraResult result = CameraResult.AccessError;
        if (Handler != null && Handler is CameraViewHandler handler)
        {
            result = await handler.StopCameraAsync();
        }
        return result;
    }
    /// <summary>
    /// Force execute the camera autofocus trigger.
    /// </summary>
    public void ForceAutoFocus()
    {
        if (Handler != null && Handler is CameraViewHandler handler)
        {
            handler.ForceAutoFocus();
        }
    }
    /// <summary>
    /// Forces the device specific control dispose
    /// </summary>
    public void ForceDisposeHandler()
    {
        if (Handler != null && Handler is CameraViewHandler handler)
        {
            handler.ForceDispose();
        }
    }
    internal void RefreshDevices()
    {
        Task.Run(() => {
            OnPropertyChanged(nameof(Cameras));
            NumCamerasDetected = Cameras.Count;
        });
    }
    public static async Task<bool> RequestPermissions(bool withMic = false, bool withStorageWrite = false)
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted) return false;
        }
        if (withMic)
        {
            status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Microphone>();
                if (status != PermissionStatus.Granted) return false;
            }
        }
        if (withStorageWrite)
        {
            status = await Permissions.CheckStatusAsync<Permissions.Media>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.CheckStatusAsync<Permissions.Media>();
                if (status != PermissionStatus.Granted)
                {
                    PermissionStatus status1 = await Permissions.RequestAsync<Permissions.Media>();
                    if (status1 != PermissionStatus.Granted) return false;
                }
            }
        }
        return true;
    }
}