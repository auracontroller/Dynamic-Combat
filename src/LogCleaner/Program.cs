using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace LogCleaner
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("LogCleaner - Bannerlord Log Filter");
            Console.WriteLine("==================================");

            string expectedErrorsFile = "ExpectedErrors.txt";
            if (!File.Exists(expectedErrorsFile))
            {
                // Also check if we are running from bin folder but file is in project root
                string altPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, expectedErrorsFile);
                if (File.Exists(altPath))
                {
                    expectedErrorsFile = altPath;
                }
                else
                {
                    Console.WriteLine($"Error: '{expectedErrorsFile}' not found in the application directory.");
                    Console.WriteLine("Please ensure 'ExpectedErrors.txt' is in the same folder as the executable.");
                    Console.WriteLine("Press any key to exit...");
                    try { Console.ReadKey(); } catch { }
                    return;
                }
            }

            Console.WriteLine($"Loading expected errors from {expectedErrorsFile}...");
            HashSet<string> expectedErrors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string line in File.ReadLines(expectedErrorsFile))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        expectedErrors.Add(line.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load ExpectedErrors.txt: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                try { Console.ReadKey(); } catch { }
                return;
            }

            Console.WriteLine($"Loaded {expectedErrors.Count} expected error signatures.");
            Console.WriteLine();
            Console.WriteLine("Please select the target log file to clean in the file dialog...");

            string targetFile = string.Empty;
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select the log file to clean";
                openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    targetFile = openFileDialog.FileName;
                }
            }

            if (string.IsNullOrEmpty(targetFile) || !File.Exists(targetFile))
            {
                Console.WriteLine("No valid file selected. Exiting...");
                return;
            }

            string directory = Path.GetDirectoryName(targetFile) ?? string.Empty;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(targetFile);
            string extension = Path.GetExtension(targetFile);

            string outputFileName = $"{fileNameWithoutExtension}_cleaned{extension}";
            string outputPath = Path.Combine(directory, outputFileName);

            Console.WriteLine($"Cleaning file: {targetFile}");

            // Regex to match Bannerlord timestamps, e.g., "[02:15:25.763] " or "[02:15:25.763]"
            Regex timestampRegex = new Regex(@"^\[\d{2}:\d{2}:\d{2}\.\d{3}\]\s*");

            int totalLines = 0;
            int removedLines = 0;

            try
            {
                using (StreamReader reader = new StreamReader(targetFile))
                using (StreamWriter writer = new StreamWriter(outputPath))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        totalLines++;

                        // Strip the timestamp to check against our dictionary
                        string strippedLine = timestampRegex.Replace(line, "").Trim();

                        if (!string.IsNullOrWhiteSpace(strippedLine) && expectedErrors.Contains(strippedLine))
                        {
                            // It's a known error, we drop it.
                            removedLines++;
                        }
                        else
                        {
                            // Not a known error (or a normal line), keep it as is (including timestamp)
                            writer.WriteLine(line);
                        }
                    }
                }

                Console.WriteLine("Clean up complete!");
                Console.WriteLine($"Total lines processed: {totalLines}");
                Console.WriteLine($"Expected errors removed: {removedLines}");
                Console.WriteLine($"Lines kept: {totalLines - removedLines}");
                Console.WriteLine($"Output saved to: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while processing the file: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            try { Console.ReadKey(); } catch { }
        }
    }
}
