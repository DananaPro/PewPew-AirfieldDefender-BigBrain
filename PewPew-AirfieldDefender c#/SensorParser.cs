using System;
using System.IO.Ports;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace VL53L7CXApp
{
    /// <summary>
    /// Represents one parsed measurement from the VL53L7CX sensor.
    /// </summary>
    public class SensorRecord
    {
        public int Line { get; set; }        // Row index (0-7)
        public int TargetIndex { get; set; } // Target number in the zone
        public int Column { get; set; }      // Column index (0-7)
        public int Distance { get; set; }    // Distance in mm, -1 = invalid
        public int Status { get; set; }      // Ranging status (0=OK, else error)
        public int Signal { get; set; }      // Signal strength (photon count)
        public int Ambient { get; set; }     // Ambient light level
        public bool IsValid => Distance > 0 && Status == 0;
    }

    /// <summary>
    /// Responsible for connecting to COM port, reading lines,
    /// and parsing them into SensorRecord objects.
    /// </summary>
    public class SensorParser : IDisposable
    {
        private readonly SerialPort _port;
        private CancellationTokenSource _cts;

        public event Action<SensorRecord> OnRecordParsed;
        public event Action<string> OnError;

        public SensorParser(string portName = "COM6", int baudRate = 460800)
        {
            _port = new SerialPort(portName, baudRate)
            {
                NewLine = "\n",
                ReadTimeout = 1000
            };
        }

        public void Start()
        {
            if (_port.IsOpen) return;
            try
            {
                _port.Open();
                _cts = new CancellationTokenSource();
                Task.Run(() => ReadLoop(_cts.Token));
                //  Block of code to try
            }
            catch (System.UnauthorizedAccessException)
            {
                Console.WriteLine("womp womp bro");            }
            catch (System.IO.FileNotFoundException ex)
            {
                // Handle FileNotFoundException
                System.Console.WriteLine($"File not found error: {ex.Message}");
            }

        }
       

        private void ReadLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    string line = _port.ReadLine()?.Trim();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var rec = ParseLine(line);
                    if (rec != null) OnRecordParsed?.Invoke(rec);
                }
                catch (TimeoutException) { /* ignore */ }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Error: {ex.Message}");
                }
            }
        }

        private SensorRecord ParseLine(string line)
        {
            try
            {
                string[] parts = line.Split(',');
                if (parts.Length < 7) return null;

                return new SensorRecord
                {
                    Line = int.Parse(parts[0]),
                    TargetIndex = int.Parse(parts[1]),
                    Column = int.Parse(parts[2]),
                    Distance = int.Parse(parts[3]),
                    Status = int.Parse(parts[4]),
                    Signal = int.Parse(parts[5]),
                    Ambient = int.Parse(parts[6])
                };
            }
            catch
            {
                // if parsing fails (junk input), skip
                return null;
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            if (_port.IsOpen) _port.Close();
        }

        public void Dispose()
        {
            Stop();
            _port.Dispose();
        }
    }
}
