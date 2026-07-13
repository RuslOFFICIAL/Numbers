using Microsoft.Maui.Controls;
using Numbers_Android.Resources;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Numbers_Android
{
    public partial class MainPage : ContentPage
    {

        // Set variables
        private readonly int maxNumber = 100000000; // Changing the value of maxNumber may affect the performance of the program! 
        private int chosenNumber;
        private int methodNumber;
        private bool awaitingMethod = true;
        private bool awaitingLang = true;

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
            WriteLine(Strings.VersionPrompt
                .Replace("\\n", Environment.NewLine)
                .Replace("{version}", version));
            WriteLine(Strings.LanguageOptionsPrompt
                .Replace("\\n", Environment.NewLine)
                .Replace("\\t", Constants.ConsoleTab));
            WriteLine(Strings.LanguagePrompt);
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

            if (awaitingLang)
            {
                bool isSupported = LanguageManager.LoadSystemLanguage(userInput);
                if (!isSupported)
                {
                    WriteLine(Strings.LanguageNeutralPrompt
                        .Replace("{lang}", userInput)
                        .Replace("\\n", Environment.NewLine));
                }
                else
                {
                    WriteLine(Strings.LanguageSettingPrompt
                        .Replace("{lang}", userInput)
                        .Replace("\\n", Environment.NewLine));
                }
                awaitingLang = false;
                WriteLine(Strings.MethodChoicePrompt.Replace("\\n", Environment.NewLine));
            }
            else if (awaitingMethod)
            {
                // MethodNumber.
                if (int.TryParse(userInput, out int m) && m >= 1 && m <= 2)
                {
                    methodNumber = m;
                    awaitingMethod = false;
                    WriteLine(Strings.EnterNumberPrompt.Replace("{maxNumber:N0}", maxNumber.ToString("N0")));
                }
                else
                {
                    WriteLine(Strings.MethodChoiceWrongPrompt.Replace("\\n", Environment.NewLine));
                }
            }
            else
            {
                // ChosenNumber.
                if (int.TryParse(userInput, out int n) && n >= 1 && n <= maxNumber)
                {
                    chosenNumber = n;
                    await RunFullProcess();
                }
                else
                {
                    WriteLine(Strings.WrongNumberPrompt.Replace("{maxNumber:N0}", maxNumber.ToString("N0")));
                    WriteLine(Strings.EnterNumberAgainPrompt);
                }

            }
        }

        private async Task RunFullProcess()
        {
            TerminalInput.IsEnabled = false;

            // Other processes.
            await ProcessNumbersAsync(chosenNumber, methodNumber);
            WriteLine("\n");
            SeparatorLine.IsVisible = true;
            await CreateFileAsync(chosenNumber, methodNumber);

            // Newxt cycle.
            awaitingMethod = true;
            TerminalInput.IsEnabled = true;
            WriteLine(Strings.NewCyclePrompt.Replace("\\n", Environment.NewLine));
        }

        private async Task ProcessNumbersAsync(int chosenNumber, int methodNumber)
        {
            WriteLine(Strings.GenerationAndOutputPrompt.Replace("\\n", Environment.NewLine));


            void FlushToUI(string text)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TerminalOutput.Text += text;
                });
            }

            await Task.Run(() =>
            {
                StringBuilder sb = new();
                int bufferedStreamSize = 1048576; // 1 MB.
                int displayLimit = 10000; // Only show first #### numbers in UI.
				int count = 0;

				switch (methodNumber)
                {
                    case 1: // Ascending.
                        for (int i = chosenNumber; i >= 1;  i++)
                        {
							if (count >= displayLimit)
                            {
								FlushToUI(sb.ToString());
								WriteLine(Strings.OutputTruncatedPrompt);
								sb.Clear();
								break;
							}

                            sb.Append(i).Append(i == chosenNumber ? "" : " ");
                            count++;
                            if (sb.Length > bufferedStreamSize)
                            {
                                FlushToUI(sb.ToString());
                                sb.Clear();
                            }
                        }
                        break;
                    case 2: // Descending.
						for (int i = chosenNumber; i >= 1; i--)
                        {
                            if (count >= displayLimit)
                            {
                                FlushToUI(sb.ToString());
                                WriteLine(Strings.OutputTruncatedPrompt);
                                sb.Clear();
                                break;
                            }
                            
                            sb.Append(i).Append(i == 1 ? "" : " ");
                            count++;
                            if (sb.Length > bufferedStreamSize)
                            {
                                 FlushToUI(sb.ToString());
                                 sb.Clear();
                            }
                        }
                        break;
                    default:
                        throw new ArgumentException(Strings.InvalidNumberPrompt);
                }

                if (sb.Length > 0)
                {
                    FlushToUI(sb.ToString());
                }
            });
        }

        private static async Task WriteToFileAsync(string filePath, int chosenNumber, int methodNumber)
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

                    switch (methodNumber)
                    {
                        case 1: // Ascending.
                            fileMemory.Span[filePos++] = (byte)'1';
                            for (int i = 2; i <= chosenNumber; i++)
                            {
                                if (filePos >= actualLength - fileMargin)
                                {
                                    await fs.WriteAsync(fileMemory[..filePos]);
                                    filePos = 0;
                                }

                                fileMemory.Span[filePos++] = spaceByte;

                                Utf8Formatter.TryFormat(i, fileMemory.Span[filePos..], out int bytesWritten);
                                filePos += bytesWritten;
                            }
                            break;
                        case 2: // Descending.
                            Utf8Formatter.TryFormat(chosenNumber, fileMemory.Span[filePos..], out int bytesWrittenDesc);
                            filePos += bytesWrittenDesc;
                            for (int i = chosenNumber - 1; i >= 1; i--)
                            {
                                if (filePos >= actualLength - fileMargin)
                                {
                                    await fs.WriteAsync(fileMemory[..filePos]);
                                    filePos = 0;
                                }

                                fileMemory.Span[filePos++] = spaceByte;
                                Utf8Formatter.TryFormat(i, fileMemory.Span[filePos..], out int bytesWritten);
                                filePos += bytesWritten;
                            }
                            break;
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

        private async Task CreateFileAsync(int chosenNumber, int methodNumber)
        {
            // Set variables.
            string timestamp = DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss");
            string targetDir = "/storage/emulated/0/R&C/Numbers/";
            string fileName = methodNumber switch
            {
                1 => $"Numbers-Android_Result_{chosenNumber}-Ascending_{timestamp}.txt",
                2 => $"Numbers-Android_Result_{chosenNumber}-Descending_{timestamp}.txt",
                _ => throw new ArgumentException(Strings.InvalidNumberPrompt),
            };
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
                        
                        // Permission check.
                        var status = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                        if (status != PermissionStatus.Granted)
                        {
                            status = await Permissions.RequestAsync<Permissions.StorageWrite>();
                        }
                        if (status != PermissionStatus.Granted)
                        {
                            WriteLine(Strings.ErrorPermissionPrompt);
                            return;
                        }

                        Directory.CreateDirectory(targetDir);
                        WriteInline(Strings.CreatingDirectorySuccessPrompt);
                    }
                    WriteLine(Strings.SaveFileProcessPrompt);
                    await Task.Run(() => WriteToFileAsync(filePath, chosenNumber, methodNumber));
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

	public static class Constants
	{
		public const string ConsoleTab = "\t";
	}
}

// All ASCII arts are from https://patorjk.com/software/taag/#p=display&f=Graffiti&t=N+++u+++m+++b+++e+++r+++s&x=none&v=4&h=4&w=80&we=false