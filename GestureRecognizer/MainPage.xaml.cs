using GestureRecognizer.Camera;
using System.Text;

namespace GestureRecognizer
{
    public partial class MainPage : ContentPage
    {
        private bool frontCamera = false;
        public MainPage()
        {
            InitializeComponent();
            cameraView.CamerasLoaded += cameraView_CamerasLoaded;
            cameraView.WidthRequest = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
            cameraView.HeightRequest = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density;
            switchCameraButton.Command = new Command(SwitchCamera);
            settingsButton.Command = new Command(OpenSettingsPage);
            AbsoluteLayout.SetLayoutBounds(rightMenuLayout, new Rect((cameraView as IView).Width - 55, 10, 70, 300));
            AbsoluteLayout.SetLayoutBounds(predictionLabel, new Rect(50, (cameraView as IView).Height - 200, 300, 100));
        }

        private void CameraViewGestureDetected(object? sender, GestureDetectionEventArgs e)
        {
            StringBuilder str = new();
            if (e.Result.Count > 0)
            {
                if (predictionLabel.Text.Split(" ").Length > 5)
                    str = str.AppendJoin(" ", e.Result);
                else
                {
                    List<string> pred = [predictionLabel.Text, .. e.Result];
                    str = str.AppendJoin(" ", pred);
                }
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    predictionLabel.Text = str.ToString();
                });
            }
        }

        private void cameraView_CamerasLoaded(object? sender, EventArgs e)
        {
            CameraStart();
            Model.Initialize();
            GestureDetectionInitialize();
        }

        private void GestureDetectionInitialize()
        {
            try
            {
                cameraView.GestureDetectionEnabled = true;
                cameraView.GestureDetected += CameraViewGestureDetected;
                cameraView.GestureProcessStarted += (s, e) => AcitvityIndicatorChange(true);
                cameraView.GestureProcessFinished += (s, e) => AcitvityIndicatorChange(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Errors.DisplayError(Errors.GESTURE_INITIALIZATION_ERROR);
            }
        }

        private void CameraStart()
        {
            try
            {
                if (cameraView.Cameras.Count > 1)
                {
                    cameraView.Camera = cameraView.Cameras[1];
                    frontCamera = !frontCamera;
                    switchCameraButton.IsVisible = true;
                }
                else
                    cameraView.Camera = cameraView.Cameras.First();

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await cameraView.StopCameraAsync();
                    await cameraView.StartCameraAsync();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Errors.DisplayError(Errors.CAMERA_START_ERROR);
            }

        }

        private void AcitvityIndicatorChange(bool isRunning)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                isBusyIndicator.IsRunning = isRunning;
            });
        }

        private void SwitchCamera()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    cameraView.GestureDetectionEnabled = false;
                    await cameraView.StopCameraAsync();
                    if (!frontCamera)
                        cameraView.Camera = cameraView.Cameras[1];
                    else
                        cameraView.Camera = cameraView.Cameras.First();
                    frontCamera = !frontCamera;
                    await cameraView.StartCameraAsync();
                    cameraView.GestureDetectionEnabled = true;
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    Errors.DisplayError(Errors.CAMERA_SWITCH_ERROR);
                }
            });
        }

        private void OpenSettingsPage()
        {
            cameraView.GestureDetectionEnabled = false;
            MainThread.BeginInvokeOnMainThread(async () => await Navigation.PushAsync(new SettingsPage(cameraView)));
        }
    }

}
