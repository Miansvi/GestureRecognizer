using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GestureRecognizer
{
    public static class Errors
    {
        public static readonly string ERROR_TITLE = "Произошла ошибка";
        public static readonly string ERROR_BUTTON_TEXT = "OK";
        public static readonly string INITIALIZATION_ERROR = "При инициализации приложения возникли проблемы";
        public static readonly string CAMERA_START_ERROR = "При запуске камеры возникли проблемы";
        public static readonly string MODEL_INITIALIZATION_ERROR = "При инициализации модели возникли проблемы";
        public static readonly string CAMERA_PERMISSION_ERROR = "Для работы приложению необходимо разрешение на использование камеры. Измените разрешения и перезайдите в приложение";
        public static readonly string GESTURE_INITIALIZATION_ERROR = "При инициализации распознавания жестов возникли проблемы";
        public static readonly string CAMERA_SWITCH_ERROR = "При переключении камеры возникли проблемы";
        public static readonly string MODEL_SWITCH_ERROR = "При переключении типа модели возникли проблемы";
        public static readonly string GESTURE_RECOGNITION_ERROR = "Ошибка при распознавании жеста";
        public static readonly string MODEL_FILE_NOT_FOUND_ERROR = "Не найден файл модели";

        public static void DisplayError(string message)
        {
            try
            {
                if (Application.Current?.MainPage != null)
                    MainThread.BeginInvokeOnMainThread(async () => await Application.Current.MainPage.DisplayAlert(ERROR_TITLE, message, ERROR_BUTTON_TEXT));
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
