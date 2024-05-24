using System.Runtime.CompilerServices;

namespace GestureRecognizer
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();
        }
    }
}
