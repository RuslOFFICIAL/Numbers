using System;
using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;
using Numbers_Windows.Resources;
using System.ComponentModel.DataAnnotations;

namespace Numbers_Windows
{
    public class Numbers_Windows_Program
    {
        public static async Task Main(string[] _)
        {
            // User culture.
            System.Globalization.CultureInfo currentCulture = System.Globalization.CultureInfo.CurrentUICulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = currentCulture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = currentCulture;
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = currentCulture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = currentCulture;
            
            
            // UI setup.
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            // Set variables.
            int maxNumber = 100000000; // Changing the value of maxNumber may affect the performance of the program!
            int chosenNumber;
            int methodNumber;
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
            Console.WriteLine(Strings.VersionPrompt
                .Replace("\\n", Environment.NewLine)
                .Replace("{version}", version)
                .Replace("{menuASCII}", menuASCII));
            LanguageManager.LoadSystemLanguage();

            // Method.
            Console.Write(Strings.MethodChoicePrompt.Replace("\\n", Environment.NewLine));

            while (!int.TryParse(Console.ReadLine(), out methodNumber) || methodNumber < 1 || methodNumber > 2)
            {
                Console.Write(Strings.MethodChoiceWrongPrompt.Replace("\\n", Environment.NewLine));
            }

            // Insert number.
            Console.Write(Strings.EnterNumberPrompt.Replace("{maxNumber:N0}", maxNumber.ToString("N0")));

            while (!int.TryParse(Console.ReadLine(), out chosenNumber) || chosenNumber < 1 || chosenNumber > maxNumber)
            {
                Console.WriteLine(Strings.WrongNumberPrompt.Replace("{maxNumber:N0}", maxNumber.ToString("N0")));
                Console.Write(Strings.EnterNumberAgainPrompt);
            }

            // Launching other processes.
            await ProcessNumbersAsync(chosenNumber, methodNumber);
            await CreateFileAsync(chosenNumber, methodNumber);

            // End.
            Console.Write(Strings.PressEnterPrompt.Replace("\\n", Environment.NewLine));
            Console.ReadLine();
        }

        private static async Task ProcessNumbersAsync(int chosenNumber, int methodNumber)
        {
            Console.WriteLine(Strings.GenerationAndOutputPrompt.Replace("\\n", Environment.NewLine));

            // Buffer.
            int bufferedStreamSize = 1048576; // 1 MB.
            int arrayPoolSize = 65536; // 64 KB.
            int displayLimit = 10000; // Only show first #### numbers in UI.
			int count = 0; 
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
						int bytesWritten;
                        const byte spaceByte = (byte)' ';

                        switch(methodNumber)
                        {
                            case 1: // Ascending
                                for (int i = 1; i <= chosenNumber; i++)
                                {
                                    if (count > displayLimit)
                                    {
                                        stdout.Write(consoleSpan[..consolePos]);
                                        consolePos = 0;
                                        byte[] msg = Encoding.UTF8.GetBytes(Strings.OutputTruncatedPrompt);
                                        stdout.Write(msg);
                                        break;
                                    }
                                    stdout.Write(consoleSpan[..consolePos]);
                                    count++;
                                    consolePos = 0;

                                    consoleSpan[consolePos++] = spaceByte;
                                    Utf8Formatter.TryFormat(i, consoleSpan[consolePos..], out bytesWritten);
                                    consolePos += bytesWritten;
                                }
                                break;
                            case 2: // Descending.
                                Utf8Formatter.TryFormat(chosenNumber, consoleSpan[consolePos..], out bytesWritten);
                                consolePos += bytesWritten;

                                for (int i = chosenNumber; i >= 1; i--)
                                {
									if (count > displayLimit)
									{
										stdout.Write(consoleSpan[..consolePos]);
										consolePos = 0;
										byte[] msg = Encoding.UTF8.GetBytes(Strings.OutputTruncatedPrompt);
										stdout.Write(msg);
										break;
									}

									stdout.Write(consoleSpan[..consolePos]);
                                    count++;
                                    consolePos = 0;
                         
                                    consoleSpan[consolePos++] = spaceByte;
                                    Utf8Formatter.TryFormat(i, consoleSpan[consolePos..], out bytesWritten);
                                    consolePos += bytesWritten;
                                }
                                break;
                            default:
                                throw new ArgumentException(Strings.InvalidNumberPrompt);
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

        private static async Task CreateFileAsync(int chosenNumber, int methodNumber)
        {
            // Set variables.
            string timestamp = DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss");
            string tempPath = Environment.GetEnvironmentVariable("TEMP") ?? Environment.CurrentDirectory;
            string targetDir = Path.Combine(tempPath, "R&C", "Numbers");
            string fileName = methodNumber switch
            {
                1 => $"Numbers-Windows_Result_{chosenNumber}-Ascending_{timestamp}.txt",
                2 => $"Numbers-Windows_Result_{chosenNumber}-Descending_{timestamp}.txt",
                _ => throw new ArgumentException(Strings.InvalidNumberPrompt),
            };
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
            Console.WriteLine(Strings.FileSizePrompt
                .Replace("{fileSizeInBytes:N0}", fileSizeInBytes.ToString("N0"))
                .Replace("{fileSizeInMB:F2}", fileSizeInMB.ToString("F2")));
            Console.WriteLine(Strings.FileDirectoryPrompt.Replace("{targetDir}", targetDir));
            Console.WriteLine(Strings.SaveFileOptionsPrompt.Replace("\\n", Environment.NewLine));
            Console.Write(Strings.SaveFileChoicePrompt.Replace("{fileName}", fileName));
            string fileResponse = Console.ReadLine()?.Trim().ToLower() ?? "";

            // Choice.
            if (fileResponse == "y" || fileResponse == "yes")
            {
                // Creating directory and file.
                try
                {
                    if (!Directory.Exists(targetDir))
                    {
                        Console.WriteLine(Strings.CreatingDirectoryPrompt.Replace("{targetDir}", targetDir));
                        Directory.CreateDirectory(targetDir);
                        Console.Write(Strings.CreatingDirectorySuccessPrompt);
                    }
                    Console.WriteLine(Strings.SaveFileProcessPrompt);
                    await Task.Run(() => WriteToFileAsync(filePath, chosenNumber, methodNumber));
                    Console.WriteLine(Strings.FilePathPrompt
                        .Replace("{filePath}", filePath)
                        .Replace("\\n", Environment.NewLine));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(Strings.CreatingDirectoryErrorPrompt.Replace("{ex.Message}", ex.Message));
                }
            }
            else
            {
                Console.WriteLine(Strings.SaveFileCancelledPrompt);
            }
        }
    }
}

// All ASCII arts are from https://patorjk.com/software/taag/#p=display&f=Graffiti&t=N+u+m+b+e+r+s&x=none&v=4&h=4&w=80&we=false