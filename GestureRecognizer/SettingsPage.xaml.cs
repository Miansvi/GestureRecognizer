using System.Text;
using System.Windows.Input;

namespace GestureRecognizer
{
    public partial class SettingsPage : ContentPage
    {
        private readonly CameraView CameraView;
        public SettingsPage(CameraView cameraView)
        {
            CameraView = cameraView;
            InitializeComponent();
            model16x4.CheckedChanged += OnModelCheckedChanged;
            model32x2.CheckedChanged += OnModelCheckedChanged;
            switch (Model.ModelName)
            {
                case Models.MVITV2_16x4:
                    CheckModel(model16x4);
                    break;
                case Models.MVITV2_32x2:
                    CheckModel(model32x2);
                    break;
                default:
                    CheckModel(model16x4);
                    break;
            }
            AbsoluteLayout.SetLayoutBounds(acitivityIndicator, new Rect((cameraView as IView).Width - 55, 10, 50, 50));
        }

        private void CheckModel(RadioButton radioButton)
        {
            radioButton.CheckedChanged -= OnModelCheckedChanged;
            radioButton.IsChecked = true;
            radioButton.CheckedChanged += OnModelCheckedChanged;
        }

        void OnModelCheckedChanged(object? sender, CheckedChangedEventArgs e)
        {
            try
            {
                if (e.Value)
                {
                    RadioButton selectedRadioButton = ((RadioButton)sender);
                    lock (CameraView.currentThreadsLocker)
                    {
                        string modelPath = Model.PickModel(Enum.Parse<Models>(selectedRadioButton.Value.ToString()));
                        Model.InitSession(modelPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Errors.DisplayError(Errors.MODEL_SWITCH_ERROR);
            }
            
        }

        protected override bool OnBackButtonPressed()
        {
            CameraView.GestureDetectionEnabled = true;
            return base.OnBackButtonPressed();
        }
    }

}
