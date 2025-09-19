using System.Collections.Concurrent;

namespace VL53L7CXApp
{
    class Program
    {
        // Store the latest frame data in a thread-safe dictionary
        private static ConcurrentDictionary<(int line, int col), SensorRecord> Frame = new();

        // Define the methods somewhere in your class
        private static void HandleRecordParsed(SensorRecord rec)
        {
            Frame[(rec.Line, rec.Column)] = rec;
        }

        private static void HandleError(string err)
        {
            Console.WriteLine($"[ERROR] {err}");
        }

        static void Main(string[] args)
        {
            using var parser = new SensorParser("COM6", 460800);

            // Subscribe using named methods
            parser.OnRecordParsed += HandleRecordParsed;
            parser.OnError += HandleError;




            //parser.OnRecordParsed += rec =>
            //{
            //    Frame[(rec.Line, rec.Column)] = rec;
            //};
            //parser.OnError += err =>
            //{
            //    Console.WriteLine($"[ERROR] {err}");
            //};
            try
            {
                parser.Start();
            }
            catch (System.IO.FileNotFoundException ex)
            {
                // Handle FileNotFoundException
                System.Console.WriteLine($"File not found error: {ex.Message}");
            }
            Console.WriteLine("Receiving data from VL53L7CX... Press Ctrl+C to stop.");

            // Analysis loop (runs in parallel)
            while (true)
            {
                Thread.Sleep(1000);

                if (Frame.TryGetValue((0, 1), out var record))
                {
                    if (record.IsValid)
                        Console.WriteLine($"Distance at (0,1): {record.Distance} mm");
                    else
                        Console.WriteLine("Measurement at (0,1) is invalid.");
                }
                else
                {
                    Console.WriteLine("No measurement recorded yet at (0,1).");
                }

            }
        }
    }
}
