using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using static AltiumTestTask.Constants;

namespace AltiumTestTask.Generator
{
    public static class Program
    {
        internal static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: size [filename]");
                Console.WriteLine("       allowed size suffix: K | M | G, for ex.: 100, 5K, 15M, 10G");
                return -1;
            }

            ulong size;
            string fileName;
            try
            {
                (size, fileName) = ParseArguments(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }

            try
            {
                CreateFile(fileName, size);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }

            return 0;
        }


        private static void CreateFile(string fileName, ulong targetSize)
        {
            using var file = File.CreateText(fileName);
            ulong size = 0;
            StringBuilder sb = new(MAX_STRING_LENGTH); // max 50  per line
            var random = new Random();
            repeatedStrings.Clear(); // just in case
            while (size < targetSize)
            {
                sb.Clear();
                // add number
                sb.Append(GenerateNumber(random));
                sb.Append(SEPARATOR);
                GenerateString(random, sb, MAX_STRING_LENGTH - sb.Length);
                file.WriteLine(sb);
                size += (ulong)sb.Length;
            }
        }

        private static List<string> repeatedStrings = new();

        private static void GenerateString(Random random, StringBuilder sb, int maxLength)
        {
            var rnd = random.NextDouble();
            if (rnd < DUPLICATE_STRING_CHANCE && repeatedStrings.Count >= DUPLICATE_STRINGS_BAG_SIZE)
            {
                sb.Append(repeatedStrings[random.Next(repeatedStrings.Count)]);
                Debug.WriteLine("Dup: " + sb);
                return;
            }

            // for start just random string of latin letters and space
            var targetLen = random.Next(maxLength - MIN_STRING_LENGTH) + MIN_STRING_LENGTH;
            // add first letter (not space) as upper
            sb.Append((char)(random.Next('Z' - 'A' + 1) + 'A'));
            var previousIsSpace = true;
            for (var i = 1; i < targetLen; i++)
            {
                var c = previousIsSpace || i == targetLen-1 ?
                    (char)(random.Next('z' - 'a' + 1) + 'a') :
                    (char)(random.Next('z' - 'a' + 5) + 'a');
                if (c > 'z') c = ' ';
                previousIsSpace = c == ' ';
                sb.Append(c);
            }

            switch (rnd)
            {
                case < DUPLICATE_STRING_CHANCE:
                {
                    // add to repeatedStrings
                    var str = sb.ToString();
                    repeatedStrings.Add(str[(str.IndexOf(SEPARATOR, StringComparison.Ordinal) + SEPARATOR.Length)..]);
                    break;
                }
                case > 1 - DUPLICATE_STRING_CHANCE when repeatedStrings.Count == DUPLICATE_STRINGS_BAG_SIZE:
                {
                    // replace random string in  list
                    var str = sb.ToString();
                    str = str[(str.IndexOf(SEPARATOR, StringComparison.Ordinal) + SEPARATOR.Length)..];
                    repeatedStrings[(int)(rnd * DUPLICATE_STRING_CHANCE)] =  str;
                    break;
                }
            }
        }

        private static int GenerateNumber(Random random)
        {
            var res = 10000.0 * -Math.Log(random.NextDouble());
            unchecked // we don't care of overflow
            {
                return (int)res;
            }
        }

        private static (ulong, string) ParseArguments(string[] args)
        {
            var argSize = args[0];
            if (string.IsNullOrWhiteSpace(argSize))
            {
                throw new ArgumentException("invalid size argument");
            }

            uint multiplier = 1;
            switch (char.ToLower(argSize[^1]))
            {
                case 'k':
                    multiplier = 1024;
                    argSize = argSize[..^1];
                    break;
                case 'm':
                    multiplier = 1024 * 1024;
                    argSize = argSize[..^1];
                    break;
                case 'g':
                    multiplier = 1024 * 1024 * 1024;
                    argSize = argSize[..^1];
                    break;
            }

            if (!ulong.TryParse(argSize, out var size))
            {
                throw new ArgumentException("invalid size argument");
            }

            checked
            {
                try
                {
                    size *= multiplier;
                }
                catch (OverflowException)
                {
                    throw new ArgumentException("size is too large");
                }
            }

            var fileName = args.Length > 1 ? args[1] : $"{DateTime.Now.Ticks}.txt";
            return (size, fileName);
        }
    }
}