using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AltiumTestTask.Sorter
{
    internal static class Program
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

            var fileLength = new FileInfo(inputFileName).Length;

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
                    var elapsed = SortFile(inputFile, outputFile);
                    Console.WriteLine($"File size: {fileLength}, elapsed: {elapsed}");
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

        private const long MAX_MEMORY_USAGE = 2L * 1024 * 1024 * 1024; // 3Gb - can we estimate by host physical memory?

        private static TimeSpan SortFile(StreamReader inputFile, TextWriter outputFile)
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();

            List<string> tmpFileNames = new();
            // suggest maximum number of lines to keep in memory
            var listCapacity = (int)Math.Min(int.MaxValue,
                MAX_MEMORY_USAGE / (Constants.MAX_STRING_LENGTH - Constants.MIN_STRING_LENGTH) / 2);
            var list = new List<LineData>(listCapacity);
            Task? sortTask = null;
            while (!inputFile.EndOfStream)
            {
                // create batch 
                long loaded = 0;
                int listIndex = 0;

                while (loaded < MAX_MEMORY_USAGE && listIndex < listCapacity && !inputFile.EndOfStream)
                {
                    var line = inputFile.ReadLine();
                    if (string.IsNullOrEmpty(line)) continue;
                    list.Add(new LineData(line));
                    listIndex++;
                    loaded += line.Length * 2L + 24; // approx.
                }


                if (inputFile.EndOfStream)
                {
                    list.Sort();
                }
                else
                {
                    var list2 = list;
                    list = new List<LineData>(listCapacity);

                    if (sortTask is { IsCompleted: false })
                    {
                        sortTask.Wait();
                    }

                    sortTask = Task.Factory.StartNew(() =>
                    {
                        list2.Sort();
                        var tmpFileName = Path.GetTempFileName();
                        using (var tmpFile = File.CreateText(tmpFileName))
                        {
                            foreach (var d in list2)
                            {
                                d.Write(tmpFile);
                            }
                        }

                        // Debug.WriteLine($"Created temp file: {tmpFileName}");
                        tmpFileNames.Add(tmpFileName);
                        list2 = null;
                        GC.Collect();
                    });
                }
            }

            Console.WriteLine($"Batches read in {stopwatch.Elapsed}");

            // input file is all read => collect all tmp and select in order and save to output
            var tmpFiles = tmpFileNames
                .Select(fileName =>
                {
                    var file = File.OpenText(fileName);
                    return new TempFileInfo(file, fileName);
                }).Union(new[] { new TempFileInfo(list) }).OrderBy(d => d.Data).ToList();
            while (tmpFiles.Count > 0)
            {
                var minFile = tmpFiles[0];
                minFile.Data?.Write(outputFile);

                if (!minFile.ReadNext())
                {
                    minFile.Delete();
                    tmpFiles.RemoveAt(0);
                }
                else
                {
                    // bubble up new line to place by order
                    var i = 1;
                    while (i < tmpFiles.Count && minFile.Data?.CompareTo(tmpFiles[i].Data) > 0)
                    {
                        (tmpFiles[i - 1], tmpFiles[i]) = (tmpFiles[i], tmpFiles[i - 1]); // swap places
                        i++;
                    }
                }
            }

            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        private class TempFileInfo
        {
            private readonly StreamReader? file;
            private List<LineData>? list;
            private int listIndex;
            private readonly string? fileName;

            public TempFileInfo(List<LineData> list)
            {
                this.list = list;
                Data = list.Count > 0 ? list[0] : null;
            }

            public TempFileInfo(StreamReader file, string fileName)
            {
                this.file = file;
                this.fileName = fileName;
                ReadNext();
            }


            public LineData? Data { get; private set; }

            public void Delete()
            {
                if (file != null)
                {
                    file.Close();
                    Debug.Assert(fileName != null, nameof(fileName) + " != null");
                    File.Delete(fileName);
                }
                else
                {
                    list = null;
                }
            }

            public bool ReadNext()
            {
                if (file != null)
                {
                    var line = file.ReadLine();
                    if (line == null)
                    {
                        Data = null;
                        return false;
                    }

                    Data = new LineData(line);
                    return true;
                }

                if (list != null)
                {
                    if (++listIndex >= list.Count)
                    {
                        Data = null;
                        return false;
                    }

                    Data = list[listIndex];
                    return true;
                }

                return false;
            }
        }

        private class LineData : IComparable<LineData>, IComparable
        {

            public LineData(string value)
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
                if (obj is not LineData other) return 1;
                return CompareTo(other);
            }

            public void Write(TextWriter file)
            {
                file.Write(NumberString);
                file.Write(Constants.SEPARATOR);
                file.WriteLine(String);
            }

            public int CompareTo(LineData? other)
            {
                var res = string.Compare(String, other?.String, StringComparison.Ordinal);
                return res == 0 && other != null ? Number.CompareTo(other.Number) : res;
            }
        }
    }
}