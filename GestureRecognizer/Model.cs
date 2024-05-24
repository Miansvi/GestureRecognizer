using Microsoft.ML.OnnxRuntime;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Size = System.Drawing.Size;

namespace GestureRecognizer
{
    public enum Models
    {
        MVITV2_16x4, 
        MVITV2_32x2,
    }
    public static class Model
    {
        private const int WIDTH = 224;
        private const int HEIGHT = 224;
        private const int CHANNEL_COUNT = 3;
        private const string MODEL_16x4_FILE_NAME = "mvit16-4.onnx";
        private const string MODEL_32x2_FILE_NAME = "mvit32-2.onnx";
        private const double THRESHOLD = 0.5;
        private const string EMPTY_STRING = "---";
        private static readonly float[] MEAN = [123.675f, 116.28f, 103.53f];
        private static readonly float[] STD = [58.395f, 57.12f, 57.375f];
        private static readonly Mat MEAN_MAT = new(WIDTH, HEIGHT, DepthType.Cv32F, 3);
        private static readonly Mat STD_MAT = new(WIDTH, HEIGHT, DepthType.Cv32F, 3);

        public static Models ModelName { get; set; } = Models.MVITV2_16x4;

        private static InferenceSession? session;
        private static InferenceSession? Session
        {
            get
            {
                if (session is null)
                {
                    string modelPath = PickModel(ModelName);
                    InitSession(modelPath);
                }
                return session;
            }
        }
        public static void InitSession(string modelPath)
        {
            if (File.Exists(modelPath))
                session = new InferenceSession(modelPath);
            else
                Errors.DisplayError(Errors.MODEL_FILE_NOT_FOUND_ERROR);
        }
        private static string? InputName => Session?.InputNames[0];
        private static int[]? InputShape => Session?.InputMetadata[InputName].Dimensions; //(1, 1, 3, 16, 224, 224)
        public static int WindowSize => InputShape != null ? InputShape[3] : 0;
        public static int FrameInterval
        {
            get
            {
                return ModelName switch
                {
                    Models.MVITV2_16x4 => 4,
                    Models.MVITV2_32x2 => 2,
                    _ => 4
                };
            }
        }
        public static void Initialize()
        {
            try
            {
                CreateMeanAndStd();
                string modelPath = PickModel(ModelName);
                InitSession(modelPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Errors.DisplayError(Errors.MODEL_INITIALIZATION_ERROR);
            }
        }

        public static string PickModel(Models model)
        {
            string modelName = model switch
            {
                Models.MVITV2_16x4 => MODEL_16x4_FILE_NAME,
                Models.MVITV2_32x2 => MODEL_32x2_FILE_NAME,
                _ => MODEL_16x4_FILE_NAME
            };
            Task.Run(async () => await ResourceFileHelper.CopyResourceFileToAppDataDirectory(modelName)).Wait();
            if (session != null)
            {
                session.Dispose();
                session = null;
            }
            ModelName = model;
            return Path.Combine(FileSystem.Current.CacheDirectory, modelName);
        }
        public static void Dispose()
        {
            Session?.Dispose();
        }
        private static void CreateMeanAndStd()
        {
            int size = WIDTH * HEIGHT * 3;
            float[] temp = new float[size];
            for (int i = 0, k = 0; i < size; i++, k++)
            {
                if (k == 3)
                    k = 0;
                temp[i] = MEAN[k];
            }
            MEAN_MAT.SetTo(temp);
            temp = new float[size];
            for (int i = 0, k = 0; i < size; i++, k++)
            {
                if (k == 3)
                    k = 0;
                temp[i] = STD[k];
            }
            STD_MAT.SetTo(temp);
        }

        private static float[][][] TransposeArray(float[,,] array)
        {
            int width = array.GetLength(0);
            int height = array.GetLength(1);
            int depth = array.GetLength(2);

            float[][][] resizedArray = new float[depth][][];
            for (int i = 0; i < depth; i++)
            {
                resizedArray[i] = new float[width][];
                for (int j = 0; j < width; j++)
                {
                    resizedArray[i][j] = new float[height];
                    for (int k = 0; k < height; k++)
                        resizedArray[i][j][k] = array[j, k, i];
                }
            }
            return resizedArray;
        }

        public static float[][][] Resize(Mat im, Size newShape)
        {
            Size shape = im.Size; // текущая размерность [height, width]

            CvInvoke.CvtColor(im, im, ColorConversion.Bgr2Rgb);
            im.ConvertTo(im, DepthType.Cv32F);

            // Коэффициент масштабирования (new / old)
            double r = Math.Min((double)newShape.Height / shape.Height, (double)newShape.Width / shape.Width); 
            // Вычисляем отступы
            Size newUnpad = new ((int)Math.Round(shape.Width * r) + 1, (int)Math.Round(shape.Height * r));
            double dw = (newShape.Width - newUnpad.Width) / 2;
            double dh = (newShape.Height - newUnpad.Height) / 2;
            if (shape != newUnpad) // Изменяем размер
                CvInvoke.Resize(im, im, newUnpad, 0, 0, Inter.Linear);
            //Добавляем границу
            int top = (int)Math.Round(dh - 0.1);
            int bottom = (int)Math.Round(dh + 0.1);
            int left = (int)Math.Round(dw - 0.1);
            int right = (int)Math.Round(dw + 0.1);
            var value = new Emgu.CV.Structure.MCvScalar(114, 114, 114);
            CvInvoke.CopyMakeBorder(im, im, top, bottom, left, right, BorderType.Constant, value);
            //Стандартизация
            CvInvoke.Subtract(im, MEAN_MAT, im);
            CvInvoke.Divide(im, STD_MAT, im);

            return TransposeArray(im.GetData() as float[,,]);
        }

        public static List<string> ProcessVideo(List<float[][][]> tensorsList)
        {
            List<string> predictionList = [];
            try
            {
                //Формируем тензор подающийся на вход в модель
                Dictionary<int, float[][]> tensors = [];
                for (int ch = 0; ch < CHANNEL_COUNT; ch++)
                    tensors[ch] = tensorsList.SelectMany(s => s[ch]).ToArray();
                var data = tensors.SelectMany(s => s.Value).SelectMany(s => s).ToArray();
                var shape = InputShape?.Select(s => (long)s).ToArray();
                long shapeCount = 1;
                for (int i = 0; i < shape?.Length; i++)
                    shapeCount *= shape[i];
                if (data.Length == (int)shapeCount)
                {
                    var inputOrt = OrtValue.CreateTensorValueFromMemory(data, shape);
                    if (InputName != null)
                    {
                        var inputs = new Dictionary<string, OrtValue>
                        {
                            { InputName, inputOrt }
                        };
                        //Получаем результат распознавания от модели
                        using OrtValue? outputs = Session?.Run(new RunOptions(), inputs, Session.OutputNames)[0];
                        //Находим индекс и макс. значение из полученного тензора
                        if (outputs != null)
                        {
                            var (value, indx) = outputs.GetTensorDataAsSpan<float>().ToArray().Select((v, i) => (v, i)).Max();
                            //Текстовое представление полученного класса
                            var gloss = Classes.Values[indx];
                            //Если полученная вероятность больше пороговой, то выводим результат
                            if (value > THRESHOLD && gloss != EMPTY_STRING && (predictionList.Count == 0 || gloss != predictionList.Last()))
                                predictionList.Add(gloss);
                        }
                    }
                }
                tensorsList = [];
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                Errors.DisplayError(Errors.GESTURE_RECOGNITION_ERROR);
            }

            return predictionList;
        }
    }
}
