using Microsoft.Maui.Controls;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Numbers_Android.Resources;

namespace Numbers_Android
{
    public partial class MainPage : ContentPage
    {

        // Set variables
        private readonly int maxNumber = 10000000; // Changing the value of maxNumber may affect the performance of the program! 

        public MainPage()
        {
            // User culture.
            System.Globalization.CultureInfo currentCulture = System.Globalization.CultureInfo.CurrentUICulture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = currentCulture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = currentCulture;

            InitializeComponent();

            // Set variables.
            var attributes = Assembly.GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .ToDictionary(a => a.Key, a => a.Value);
            var versionNumber = attributes.GetValueOrDefault("ApplicationDisplayVersion", "Unknown");
            var versionOnly = attributes.GetValueOrDefault("VersionOnly", "Unknown");
            var platform = attributes.GetValueOrDefault("PlatformString", "Unknown");

            // Version.
            string versionOnlyString = versionOnly switch
            {
                "1" => $"-{platform}",
                "0" => "",
                _ => "-Unknown"
            };
            string version = $"{versionNumber}{versionOnlyString}";

            // Print text.
            WriteLine($"Version {version} Android\n\n");
            WriteLine(Strings.EnterNumberPrompt.Replace("{maxNumber:N0}", maxNumber.ToString("N0")));
        }
        private void WriteLine(string text)
        {
            //Console.WriteLine() command.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                TerminalOutput.Text += text + Environment.NewLine;
                TerminalScroll.ScrollToAsync(TerminalOutput, ScrollToPosition.End, true);
            });
        }

        private void WriteInline(string text)
        {
            // Console.Write() command.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                TerminalOutput.Text += text;
                TerminalScroll.ScrollToAsync(TerminalOutput, ScrollToPosition.End, false);
            });
        }

        private async void OnInputSubmitted(object sender, EventArgs e)
        {
            // Insert number.
            string userInput = TerminalInput.Text?.Trim() ?? "";
            TerminalInput.Text = string.Empty;

            WriteInline($"> {userInput}\n");

            if (!int.TryParse(userInput, out int chosenNumber) || chosenNumber < 1 || chosenNumber > maxNumber)
            {
                WriteLine(Strings.WrongNumberPrompt.Replace("{maxNumber:N0}", maxNumber.ToString("N0")));
                WriteLine(Strings.EnterNumberAgainPrompt);
                return;
            }

            TerminalInput.IsEnabled = false;

            // Launching other processes.
            await ProcessNumbersAsync(chosenNumber);
            WriteLine("\n");
            SeparatorLine.IsVisible = true;
            await CreateFileAsync(chosenNumber);

            TerminalInput.IsEnabled = true;
            WriteLine(Strings.NewCyclePrompt.Replace("\\n", Environment.NewLine));
        }

        private async Task ProcessNumbersAsync(int chosenNumber)
        {
            WriteLine(Strings.GenerationAndOutputPrompt.Replace("\\n", Environment.NewLine));

            // Buffer.
            int bufferedStreamSize = 1048576; // 1 MB.
            int margin = 32; // 32 Bytes. Safe margin for Utf8Formatter.
            await Task.Run(() =>
            {
                byte[] pooledBuffer = ArrayPool<byte>.Shared.Rent(bufferedStreamSize);

                try
                {
                    Span<byte> consoleSpan = pooledBuffer.AsSpan();
                    int actualLength = consoleSpan.Length;
                    int consolePos = 0;

                    const byte spaceByte = (byte)' ';
                    consoleSpan[consolePos++] = (byte)'1';

                    for (int i = 2; i <= chosenNumber; i++)
                    {
                        if (consolePos >= actualLength - margin)
                        {
                            string chunk = Encoding.UTF8.GetString(pooledBuffer, 0, consolePos);
                            MainThread.BeginInvokeOnMainThread(() => {
                                TerminalOutput.Text += chunk;
                            });
                            consolePos = 0;
                        }

                        consoleSpan[consolePos++] = spaceByte;
                        Utf8Formatter.TryFormat(i, consoleSpan[consolePos..], out int bytesWritten);
                        consolePos += bytesWritten;
                    }

                    if (consolePos > 0)
                    {
                        string chunk = Encoding.UTF8.GetString(pooledBuffer, 0, consolePos);
                        WriteInline(chunk);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(pooledBuffer);
                }
            });
        }

        private static async Task WriteToFileAsync(string filePath, int chosenNumber)
        {
            // Set variables
            int internalStreamBuffer = 4096; // 4 KB.
            int fileArrayPoolSize = 1048576; // 1 MB buffer.
            int fileMargin = 32; // 32 Bytes. Safe padding for Utf8Formatter.

            // File parameters.
            FileStreamOptions options = new()
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = internalStreamBuffer,
                Options = FileOptions.Asynchronous
            };

            // Optimization.
            using FileStream fs = File.Open(filePath, options);
            {
                byte[] pooledBuffer = ArrayPool<byte>.Shared.Rent(fileArrayPoolSize);

                try
                {
                    Memory<byte> fileMemory = pooledBuffer.AsMemory();
                    int actualLength = fileMemory.Length;
                    int filePos = 0;

                    const byte spaceByte = (byte)' ';
                    Span<byte> initialSpan = fileMemory.Span;
                    initialSpan[filePos++] = (byte)'1';

                    for (int i = 2; i <= chosenNumber; i++)
                    {
                        if (filePos >= actualLength - fileMargin)
                        {
                            await fs.WriteAsync(fileMemory[..filePos]);
                            filePos = 0;
                        }

                        Span<byte> currentSpan = fileMemory.Span;
                        currentSpan[filePos++] = spaceByte;

                        Utf8Formatter.TryFormat(i, currentSpan[filePos..], out int bytesWritten);
                        filePos += bytesWritten;
                    }

                    if (filePos > 0)
                    {
                        await fs.WriteAsync(fileMemory[..filePos]);
                    }
                    await fs.FlushAsync();
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(pooledBuffer);
                }
            }
        }

        private static long CalculateExpectedFileSize(int chosenNumber)
        {
            if (chosenNumber <= 0) return 0;

            // Calculating the size of the file mathematically.
            long totalBytes = 0;
            long start = 1;
            int digits = 1;
            while (start <= chosenNumber)
            {
                long end = Math.Min(chosenNumber, start * 10 - 1);
                long count = end - start + 1;
                totalBytes += count * digits;
                start *= 10;
                digits++;
            }
            totalBytes += (chosenNumber - 1);
            return totalBytes;
        }

        private async Task CreateFileAsync(int chosenNumber)
        {
            // Set variables.
            string timestamp = DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss");
            string targetDir = "/storage/emulated/0/R&C/Numbers/";
            string fileName = $"Numbers-Result_{chosenNumber}_{timestamp}.txt";
            string filePath = Path.Combine(targetDir, fileName);

            // Calculate file size.
            long fileSizeInBytes = CalculateExpectedFileSize(chosenNumber);
            double fileSizeInMB = fileSizeInBytes / (1024.0 * 1024.0);

            // Choice.
            bool shouldSave = await DisplayAlert
                (Strings.SaveFilePrompt,
                Strings.FileSizePrompt
                    .Replace("{fileSizeInBytes:N0}", fileSizeInBytes.ToString("N0"))
                    .Replace("{fileSizeInMB:F2}", fileSizeInMB.ToString("F2")) +
                Strings.FileDirectoryPrompt.Replace("{targetDir}", targetDir) +
                Strings.SaveFileChoicePrompt.Replace("{fileName}", fileName),
                Strings.YesPrompt,
                Strings.NoPrompt);
            if (shouldSave)
            {
                try
                {
                    if (!Directory.Exists(targetDir))
                    {
                        WriteLine(Strings.CreatingDirectoryPrompt.Replace("{targetDir}", targetDir));
                        Directory.CreateDirectory(targetDir);
                        WriteInline(Strings.CreatingDirectorySuccessPrompt);
                    }
                    WriteLine(Strings.SaveFileProcessPrompt);
                    await Task.Run(() => WriteToFileAsync(filePath, chosenNumber));
                    WriteLine(Strings.FilePathPrompt
                        .Replace("{filePath}", filePath)
                        .Replace("\\n", Environment.NewLine));
                }
                catch (Exception ex)
                {
                    WriteLine(Strings.CreatingDirectoryErrorPrompt
                        .Replace("{ex.Message}", ex.Message)
                        .Replace("\\n", Environment.NewLine));
                    WriteLine(Strings.ErrorAccessPrompt);
                }
            }
            else
            {
                WriteLine(Strings.SaveFileCancelledPrompt);
            }
        }
    }
}

// All ASCII arts are from https://patorjk.com/software/taag/#p=display&f=Graffiti&t=N+++u+++m+++b+++e+++r+++s&x=none&v=4&h=4&w=80&we=false