using Microsoft.Maui.Controls;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Numbers_Android
{
    public partial class MainPage : ContentPage
    {
        // Set variables
        private readonly int maxNumber = 10000000; // Changing the value of maxNumber may affect the performance of the program! 
        
        private static string GetVersionFromProject()
        {
            // Change the version of the program only in Numbers_Android.csproj!
            var assembly = Assembly.GetExecutingAssembly();
            var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            return metadata.FirstOrDefault(m => m.Key == "ApplicationDisplayVersion")?.Value ?? "Unknown";
        }

        public MainPage()
        {

            InitializeComponent();
            string version = GetVersionFromProject();
            
            // Print text.
            WriteLine($"Version {version} Android\n\n");
            WriteLine($"Введіть число (повинно бути в діапазоні від 1 до {maxNumber:N0}): ");
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
                WriteLine($"Число не підходить (повинно бути в діапазоні від 1 до {maxNumber:N0}). Спробуйте знову.");
                WriteLine("Введіть число: ");
                return;
            }

            TerminalInput.IsEnabled = false;

            // Launching other processes.
            await ProcessNumbersAsync(chosenNumber);
            WriteLine("\n");
            SeparatorLine.IsVisible = true;
            await CreateFileAsync(chosenNumber);

            TerminalInput.IsEnabled = true;
            WriteLine("\nГотово! Введіть наступне число для нового циклу:");
        }

        private async Task ProcessNumbersAsync(int chosenNumber)
        {
            WriteLine("\nГенерація та вивід чисел у консоль. Зачекайте...");

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
                            WriteInline(chunk);
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
            bool shouldSave = await DisplayAlert("Зберегти файл?", $"Розмір файлу: {fileSizeInBytes:N0} байт ({fileSizeInMB:F2} MB)\nФайл буде збережено в {targetDir}\nЗберегти ці числа у файл {fileName}?", "Так", "Ні");
            if (shouldSave)
            {
                try
                {
                    WriteLine($"Створення папки {targetDir}...");
                    Directory.CreateDirectory(targetDir);
                    WriteLine("Запис у файл...");
                    await WriteToFileAsync(filePath, chosenNumber);
                    WriteLine($"Успішно збережено! Шлях до файлу:\n{filePath}");
                }
                catch (Exception ex)
                {
                    WriteLine($"Помилка при роботі з файловою системою: {ex.Message}");
                    WriteLine($"Спробуйте надати програмі Numbers дозвіл на доступ до усіх файлів.");
                }
            }
            else
            {
                WriteLine("Запис скасовано.");
            }
        }
    }
}

// All ASCII arts are from https://patorjk.com/software/taag/#p=display&f=Graffiti&t=N+++u+++m+++b+++e+++r+++s&x=none&v=4&h=4&w=80&we=false