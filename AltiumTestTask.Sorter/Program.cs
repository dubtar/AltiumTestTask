using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AltiumTestTask.Sorter
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: inputFileName outputFileName");
                return -1;
            }

            string inputFileName = args[0], outputFileName = args[1];

            StreamReader inputFile;
            try
            {
                inputFile = File.OpenText(inputFileName);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error opening input file: {ex.Message}");
                return 1;
            }

            StreamWriter? outputFile = null;
            try
            {
                try
                {
                    outputFile = File.CreateText(outputFileName);

                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error opening output file: {ex.Message}");
                    return 1;
                }

                try
                {
                    SortFile(inputFile, outputFile);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    return 2;
                }
            }
            finally
            {
                inputFile.Close();
                outputFile?.Close();
            }

            return 0;
        }

        private const long MAX_MEMORY_USAGE = 8L * 1024 * 1024 * 1024; // 8Gb
        private static void SortFile(StreamReader inputFile, TextWriter outputFile)
        {
            List<string> tmpFileNames = new();
            while (!inputFile.EndOfStream)
            {
               // create batch 
               long loaded = 0;
               var list = new List<Data>();
#if DEBUG
                Debug.Write($"Memory before read: {GC.GetTotalMemory(false)}");
#endif
               while (loaded < MAX_MEMORY_USAGE && !inputFile.EndOfStream)
               {
                   var line = inputFile.ReadLine();
                   if (string.IsNullOrEmpty(line)) continue;
                   list.Add(new Data(line));
                   loaded += line.Length * 2L + 24; // approx.
               }
               
#if DEBUG
                Debug.WriteLine($",  after: {GC.GetTotalMemory(false)}");
#endif
               list.Sort();
               var tmpFileName = Path.GetTempFileName();
               using (var tmpFile = File.CreateText(tmpFileName))
               {
                   foreach (var d in list)
                   {
                       d.Write(tmpFile);
                   }
               }
               Debug.WriteLine($"Created temp file: {tmpFileName}");
               tmpFileNames.Add(tmpFileName);
               list.Clear();
               GC.Collect();
            }
            // input file is all read => collect all tmp and select in order and save to output
            var tmpFiles = tmpFileNames.Select(fileName =>
            {
                var file = File.OpenText(fileName);
                var data = new Data(file.ReadLine() ?? throw new InvalidOperationException("Temp file is empty"));
                return new TempFileInfo(file, data, fileName);
            }).OrderBy(d => d.data).ToList();
            while (tmpFiles.Count > 0)
            {
                var minFile = tmpFiles[0];
                minFile.data.Write(outputFile);
                
                var newline = minFile.file.ReadLine();
                if (newline == null)
                {
                    minFile.file.Close();
                    File.Delete(minFile.fileName);
                    tmpFiles.RemoveAt(0);
                }
                else
                {
                    minFile.data = new Data(newline);
                    // bubble up new line to place by order
                    var i = 1;
                    while (i < tmpFiles.Count && minFile.data.CompareTo(tmpFiles[i].data) > 0)
                    {
                        (tmpFiles[i - 1], tmpFiles[i]) = (tmpFiles[i], tmpFiles[i - 1]); // swap places
                        i++;
                    }
                }
            }
            
        }

        private class TempFileInfo
        {
            public readonly StreamReader file;
            public Data data;
            public readonly string fileName;

            public TempFileInfo(StreamReader file, Data data, string fileName)
            {
                this.file = file;
                this.data = data;
                this.fileName = fileName;
            }
        }

        private class Data: IComparable
        {
            public Data(string value)
            {
                var sepIndex = value.IndexOf(Constants.SEPARATOR, StringComparison.Ordinal);
                if (sepIndex == -1)
                {
                    Number = 0;
                    NumberString = "";
                    String = value;
                }
                else
                {
                    NumberString = value[..sepIndex];
                    int.TryParse(NumberString, out Number);
                    String = value[(sepIndex + Constants.SEPARATOR.Length)..];
                }

            }

            private readonly int Number;
            private readonly string NumberString;
            private readonly string String;
            
            public int CompareTo(object? obj)
            {
                if (obj is not Data b) return 1;
                var res = string.Compare(String, b.String, StringComparison.Ordinal);
                return res == 0 ? Number.CompareTo(b.Number) : res;
            }

            public void Write(TextWriter file)
            {
                file.Write(NumberString);
                file.Write(Constants.SEPARATOR);
                file.WriteLine(String);
            }
        }
    }
}