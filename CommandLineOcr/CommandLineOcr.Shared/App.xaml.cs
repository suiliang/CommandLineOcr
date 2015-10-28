using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using WindowsPreview.Media.Ocr;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.Serialization.Json;
using System.Text;
using Windows.Data.Json;
// The Blank Application template is documented at http://go.microsoft.com/fwlink/?LinkId=234227

namespace CommandLineOcr
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public sealed partial class App : Application
    {
#if WINDOWS_PHONE_APP
        private TransitionCollection transitions;
#endif

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += this.OnSuspending;
        }

        protected async override void OnLaunched(LaunchActivatedEventArgs e)
        {
            StorageFolder folder = Windows.Storage.ApplicationData.Current.LocalFolder;
            if (!string.IsNullOrEmpty(e.Arguments))
            {
                try
                {
                    Debug.WriteLine("args: " + e.Arguments);
                    var ocrEngine = new OcrEngine(OcrLanguage.English);
                    var file = await folder.GetFileAsync(e.Arguments);
                    ImageProperties imgProp = await file.Properties.GetImagePropertiesAsync();
                    WriteableBitmap bitmap;
                    using (var imgStream = await file.OpenAsync(FileAccessMode.Read))
                    {
                        bitmap = new WriteableBitmap((int)imgProp.Width, (int)imgProp.Height);
                        bitmap.SetSource(imgStream);
                    }
                    // Check whether is loaded image supported for processing.
                    // Supported image dimensions are between 40 and 2600 pixels.
                    if (bitmap.PixelHeight < 40 ||
                        bitmap.PixelHeight > 2600 ||
                        bitmap.PixelWidth < 40 ||
                        bitmap.PixelWidth > 2600)
                    {
                        //write invalid image to output

                        return;
                    }

                    // This main API call to extract text from image.
                    var ocrResult = await ocrEngine.RecognizeAsync((uint)bitmap.PixelHeight, (uint)bitmap.PixelWidth, bitmap.PixelBuffer.ToArray());

                    // OCR result does not contain any lines, no text was recognized. 
                    if (ocrResult.Lines != null)
                    {
                        JsonObject jsonOjbect = new JsonObject();
                        jsonOjbect.Add("text_angle", JsonValue.CreateNumberValue(ocrResult.TextAngle.HasValue ? ocrResult.TextAngle.Value : 0d));

                        JsonArray wordsArray = new JsonArray();
                        jsonOjbect.Add("words", wordsArray);

                        // Iterate over recognized lines of text.
                        foreach (var line in ocrResult.Lines)
                        {
                            foreach (var word in line.Words)
                            {
                                JsonObject wordJson = new JsonObject();
                                wordsArray.Add(wordJson);
                                wordJson.Add("top", JsonValue.CreateNumberValue(word.Top));
                                wordJson.Add("left", JsonValue.CreateNumberValue(word.Left));
                                wordJson.Add("width", JsonValue.CreateNumberValue(word.Width));
                                wordJson.Add("height", JsonValue.CreateNumberValue(word.Height));
                                wordJson.Add("text", JsonValue.CreateStringValue(word.Text));
                            }
                        }
                        await WriteToFile(folder, file.Name + ".txt", jsonOjbect.Stringify());
                    }
                    else
                    {

                        await WriteToFile(folder, "failed.txt", "No Text");
                    }

                }
                catch (Exception ex)
                {
                    await WriteToFile(folder, "failed.txt", ex.Message + "\r\n"+ex.StackTrace);
                }
                App.Current.Exit();
            }
        }

        private async Task WriteToFile(StorageFolder folder, string fileName,string extractedText)
        {
            // Get the text data from the textbox. 
            byte[] fileBytes = System.Text.Encoding.UTF8.GetBytes(extractedText.ToCharArray());

            // Create a new file named DataFile.txt.
            var file = await folder.CreateFileAsync(fileName,
            CreationCollisionOption.ReplaceExisting);

            // Write the data from the textbox.
            using (var s = await file.OpenStreamForWriteAsync())
            {
                s.Write(fileBytes, 0, fileBytes.Length);
            }
        }

#if WINDOWS_PHONE_APP
        /// <summary>
        /// Restores the content transitions after the app has launched.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the navigation event.</param>
        private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e)
        {
            var rootFrame = sender as Frame;
            rootFrame.ContentTransitions = this.transitions ?? new TransitionCollection() { new NavigationThemeTransition() };
            rootFrame.Navigated -= this.RootFrame_FirstNavigated;
        }
#endif

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            // TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}