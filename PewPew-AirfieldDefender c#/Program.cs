using System.Collections.Concurrent;
using System.IO.Ports;

namespace VL53L7CXApp
{
    class Program
    {
        // Thread-safe storage for sensor data
        private static ConcurrentDictionary<(int line, int col), SensorRecord> Frame = new();
        private static ConcurrentDictionary<(int line, int col), SensorRecord> CeilingFrame = new();

        // ESP32 serial connection
        private static SerialPort esp32;

        // Keep track of active lasers to avoid spamming commands
        private static ConcurrentDictionary<(int, int), bool> ActiveTargets = new();

        static void Main(string[] args)
        {
            // Open ESP32 serial port
            esp32 = new SerialPort("COM7", 115200); // Change to your ESP32 port
            try
            {
                esp32.Open();
            }
            catch (System.IO.FileNotFoundException)
            {
                Console.WriteLine("you need to connect it 💀");
            }

            // VL53L7CX sensor parser
            using var parser = new SensorParser("COM6", 460800);
            parser.OnRecordParsed += HandleRecordParsed;
            parser.OnError += HandleError;

            try
            {
                parser.Start();
            }
            catch (System.IO.FileNotFoundException ex)
            {
                Console.WriteLine($"File not found error: {ex.Message}");
                return;
            }

            Console.WriteLine("Receiving data from VL53L7CX... Press Ctrl+C to stop.");

            // Wait until a full initial frame is received
            while (Frame.Count < 64)
                Thread.Sleep(10);

            // Copy Frame to CeilingFrame
            foreach (var pair in Frame)
                CeilingFrame[pair.Key] = pair.Value;

            Console.WriteLine("Ceiling snapshot taken!");

            // Main detection loop
            while (true)
            {
                for (int i = 0; i < 8; i++)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        if (Frame.TryGetValue((i, j), out var record) && record.IsValid &&
                            CeilingFrame.TryGetValue((i, j), out var ceiling) && ceiling.IsValid)
                        {
                            int diff = ceiling.Distance - record.Distance;
                            if (diff > 150) // object detected under ceiling
                            {
                                var key = (i, j);
                                if (!ActiveTargets.ContainsKey(key))
                                {
                                    Console.WriteLine($"Target Locked at ({i},{j}) {diff} mm under ceiling!");

                                    // Send command to ESP32
                                    string cmd = $"FIRE {i}{j}\n";
                                    try
                                    {
                                        esp32.Write(cmd);
                                    }
                                    catch (System.InvalidOperationException)
                                    {
                                        Console.WriteLine("you need to connect it 💀");
                                    }

                                    ActiveTargets[key] = true;
                                }
                            }
                            else
                            {
                                // Remove target if no longer detected
                                ActiveTargets.TryRemove((i, j), out _);
                            }
                        }
                    }
                }

                Thread.Sleep(100); // 10 Hz update
            }
        }

        private static void HandleRecordParsed(SensorRecord rec)
        {
            Frame[(rec.Line, rec.Column)] = rec;
        }

        private static void HandleError(string err)
        {
            Console.WriteLine($"[ERROR] {err}");
        }
    }
}
