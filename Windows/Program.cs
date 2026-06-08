using System;
using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;

namespace Numbers_Windows
{
    public class Numbers_Windows_Program
    {
        public static async Task Main(string[] _)
        {
            // Localization.
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            // Set variables.
            int maxNumber = 100000000; // Changing the value of maxNumber may affect the performance of the program!
            int chosenNumber;
            string menuASCII = " _______                      ___.                               \r\n \\      \\    __ __    _____   \\_ |__     ____   _______    ______\r\n /   |   \\  |  |  \\  /     \\   | __ \\  _/ __ \\  \\_  __ \\  /  ___/\r\n/    |    \\ |  |  / |  Y Y  \\  | \\_\\ \\ \\  ___/   |  | \\/  \\___ \\ \r\n\\____|__  / |____/  |__|_|  /  |___  /  \\___  >  |__|    /____  >\r\n        \\/                \\/       \\/       \\/                \\/ ";
            var attributes = Assembly.GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .ToDictionary(a => a.Key, a => a.Value);
            var versionNumber = attributes.GetValueOrDefault("AppVersion", "Unknown");
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
            Console.WriteLine($"{menuASCII}\nVersion {version}\n\n");

            // Insert number.
            Console.Write($"Введіть число (повинно бути в діапазоні від 1 до {maxNumber:N0}): ");

            while (!int.TryParse(Console.ReadLine(), out chosenNumber) || chosenNumber < 1 || chosenNumber > maxNumber)
            {
                Console.WriteLine($"Число не підходить (повинно бути в діапазоні від 1 до {maxNumber:N0}). Спробуйте знову.");
                Console.Write("Введіть число: ");
            }

            // Launching other processes.
            await ProcessNumbersAsync(chosenNumber);
            await CreateFileAsync(chosenNumber);

            // End.
            Console.WriteLine("\nНатисніть [Enter] щоб закрити програму...");
            Console.ReadLine();
        }

        private static async Task ProcessNumbersAsync(int chosenNumber)
        {
            Console.WriteLine("\nГенерація та вивід чисел у консоль. Зачекайте...");

            // Buffer.
            int bufferedStreamSize = 1048576; // 1 MB.
            int arrayPoolSize = 65536; // 64 KB.
            int margin = 32; // 32 Bytes. Safe margin for Utf8Formatter.
            await Task.Run(() =>
            {
                using Stream rawStdout = Console.OpenStandardOutput();
                using BufferedStream stdout = new(rawStdout, bufferedStreamSize);
                {
                    byte[] pooledBuffer = ArrayPool<byte>.Shared.Rent(arrayPoolSize);

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
                                stdout.Write(consoleSpan[..consolePos]);
                                consolePos = 0;
                            }

                            consoleSpan[consolePos++] = spaceByte;
                            Utf8Formatter.TryFormat(i, consoleSpan[consolePos..], out int bytesWritten);
                            consolePos += bytesWritten;
                        }

                        if (consolePos > 0)
                        {
                            stdout.Write(consoleSpan[..consolePos]);
                        }
                        stdout.Flush();
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(pooledBuffer);
                    }
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
                byte[] pooledBuffer = ArrayPool<byte>.Shared.Rent(fileArrayPoolSize); // 1 MB

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

        private static async Task CreateFileAsync(int chosenNumber)
        {
            // Set variables.
            string timestamp = DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss");
            string tempPath = Environment.GetEnvironmentVariable("TEMP") ?? Environment.CurrentDirectory;
            string targetDir = Path.Combine(tempPath, "R&C", "Numbers");
            string fileName = $"Numbers-Result_{chosenNumber}_{timestamp}.txt";
            string filePath = Path.Combine(targetDir, fileName);

            // Calculate file size.
            long fileSizeInBytes = CalculateExpectedFileSize(chosenNumber);
            double fileSizeInMB = fileSizeInBytes / (1024.0 * 1024.0);

            // Separator.
            Console.WriteLine();
            int width = Console.WindowWidth;
            string separator = new('-', Math.Max(0, width - 1));
            Console.WriteLine(separator);

            // Make file.
            Console.WriteLine($"Розмір файлу: {fileSizeInBytes:N0} байт ({fileSizeInMB:F2} MB)");
            Console.WriteLine($"Файл буде збережено в {targetDir}");
            Console.WriteLine("\n\nY/yes = Записати у файл | Інша клавіша = Не записувати");
            Console.Write($"Зберегти ці числа у файл {fileName}? ");
            string fileResponse = Console.ReadLine()?.Trim().ToLower() ?? "";

            // Choice.
            if (fileResponse == "y" || fileResponse == "yes")
            {
                // Creating directory and file.
                try
                {
                    Console.WriteLine($"Створення папки {targetDir}...");
                    Directory.CreateDirectory(targetDir);
                    Console.WriteLine("Папка успішно створена! Запис у файл...");
                    await WriteToFileAsync(filePath, chosenNumber);
                    Console.WriteLine($"Успішно збережено! Шлях до файлу:\n{filePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Помилка при роботі з файловою системою: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Запис скасовано.");
            }
        }
    }
}

// All ASCII arts are from https://patorjk.com/software/taag/#p=display&f=Graffiti&t=N+u+m+b+e+r+s&x=none&v=4&h=4&w=80&we=false