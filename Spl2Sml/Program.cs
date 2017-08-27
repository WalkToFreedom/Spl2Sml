using System;

namespace Spl2Sml
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("Use the bat file that came with this app! Otherwise you can use this app two ways:\r\n" +
                    "Process all files one off use: .exe [source] [dest] [-all] [playTimeOffset(milliseconds)]\r\n" +
                    "Run periodically: .exe [source] [dest] [interval(mins)] [playTimeOffset(milliseconds)]");
                return;
            }

            var convertAll = args[2] == "-all";

            var interval = 2; // mins

            if (!convertAll)
                interval = int.Parse(args[2]);

            var playtimeOffset = int.Parse(args[3]);

            new Converter(args[0], args[1], convertAll, interval, playtimeOffset);

            while (true) { }
        }
    }
}
