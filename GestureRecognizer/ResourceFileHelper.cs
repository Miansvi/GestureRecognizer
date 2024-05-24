namespace GestureRecognizer
{
    public static class ResourceFileHelper
    {
        public static async Task CopyResourceFileToAppDataDirectory(string fileName)
        {
            // Open the source file
            using Stream inputStream = await FileSystem.Current.OpenAppPackageFileAsync(fileName);

            // Create an output filename
            string targetFile = Path.Combine(FileSystem.Current.CacheDirectory, fileName);
            if (!File.Exists(targetFile))
            {
                // Copy the file to the AppDataDirectory
                using FileStream outputStream = File.Create(targetFile);
                inputStream.CopyTo(outputStream);
            }
        }
    }
}
