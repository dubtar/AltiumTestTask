using System;
using System.IO;
using System.Text;

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

        private const string SEPARATOR = ". ";
        private const int MIN_STRING_LENGTH = 5;
        private const int MAX_STRING_LENGTH = 50;
        private static void CreateFile(string fileName, ulong targetSize)
        {
            using var file = File.CreateText(fileName);
            ulong size = 0;
            StringBuilder sb = new(MAX_STRING_LENGTH); // max 50  per line
            var random = new Random();
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

        private static void GenerateString(Random random, StringBuilder sb, int maxLength)
        {
            // for start just random string of latin letters and space
            var targetLen = random.Next(maxLength - MIN_STRING_LENGTH) + MIN_STRING_LENGTH;
            // add first letter (not space) as upper
            sb.Append((char)(random.Next('Z' - 'A' + 1) + 'A'));
            for (var i = 1; i < targetLen; i++)
            {
                var c = (char)(random.Next('z' - 'a' + 5) + 'a');
                if (c > 'z') c = ' ';
                sb.Append(c);
            }
        }

        private static int GenerateNumber(Random random)
        {
            // a bit modified version of Normal distribution generator found on SO
            const double DEVIATION = 10 ^ 5; // mean is 0
            double u1 = 1.0 - random.NextDouble();
            double u2 = 1.0 - random.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            var res = Math.Abs(Math.Floor(randStdNormal * DEVIATION));
            if (res > int.MaxValue) res %= int.MaxValue;
            return (int)res;
        }

        private static (ulong, string) ParseArguments(string[] args)
        {
            var argSize  = args[0];
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