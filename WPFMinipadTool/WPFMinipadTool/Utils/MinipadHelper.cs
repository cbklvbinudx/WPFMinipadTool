using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MinipadWPFTest.Models;

namespace MinipadWPFTest.Utils
{
    public static class MinipadHelper
    {
        public static async Task<Dictionary<string,string>> GetMinipad(int comport)
        {
            Dictionary<string, string> values = new Dictionary<string, string>();
            try
            {
                // Open the serial port.
                SerialPort serialPort = new SerialPort($"COM{comport}", 115200, Parity.Even, 8, StopBits.One);
                serialPort.RtsEnable = true;
                serialPort.DtrEnable = true;
                serialPort.Open();

                // Create a semaphore for timeouting the serial data reading.
                SemaphoreSlim semaphoreSlim = new SemaphoreSlim(0);

                // Callback method for processing the data received from the serial monitor.
                void DataReceivedCallback(object sender, SerialDataReceivedEventArgs e)
                {
                    // Lock the serial port to make sure it's not being closed when the 200ms timeout runs out.
                    lock (serialPort)
                    {
                        // Read the data all the way to the end, line by line.
                        while (serialPort.BytesToRead > 0)
                        {
                            // Read the next line.
                            string line = serialPort.ReadLine().TrimEnd('\r');

                            // If the end of the 'get' command was reached (indicated by "GET END"), release the semaphore thus returning.
                            if (line == "GET END")
                                semaphoreSlim.Release();
                            // If the received line starts with "GET", it's a key-value pair with the keypad's specifications.
                            else if (line.StartsWith($"GET "))
                            {
                                // Add the key and value as a string to the values dictionary.
                                string key = line[4..].Split('=')[0];
                                values.Add(key, line[(4 + key.Length + 1)..]);
                            }
                        }
                    }
                }

                // Subscribe to the callback method, write the 'get' command that returns the keypad specifications.
                serialPort.DataReceived += DataReceivedCallback;
                serialPort.WriteLine("get");

                // Give the response 200ms until this reading process times out.
                await semaphoreSlim.WaitAsync(10);

                // Safely close the serial port for usage by other processes.
                lock (serialPort)
                    serialPort.Close();
                values.Add("state", "connected");
                return values;
            }
            catch (UnauthorizedAccessException ex)
            {
                // If an UnauthorizedAccessException was thrown, the device is connected but the serial interface occupied by another process.
                values.Add("state", "busy");
                return values;
            }
            catch (FileNotFoundException ex)
            {
                // If a FileNotFoundException was thrown, the device is disconnected.
                values.Add("state", "disconnected");
                return values;
            }
        }

        public static async Task<int> SendCommand(int comport, string command)
        {
            var minipadValues = await GetMinipad(comport);
            if (minipadValues["state"] == "connected")
            {
                try
                {
                    // Open the serial port.
                    SerialPort serialPort = new SerialPort($"COM{comport}", 115200, Parity.Even, 8, StopBits.One);
                    serialPort.RtsEnable = true;
                    serialPort.DtrEnable = true;
                    serialPort.Open();

                    // Write command to the serial interface.
                    serialPort.WriteLine(command);

                    // Safely close the serial port for usage by other processes.
                    serialPort.Close();
                }
                catch (Exception ex)
                {
                    // Failed to send command
                    return 2;
                }
                return 0;
            }
            else
            {
                // Busy/disconnected
                return 1;
            }
        }
        public static async Task<int> SendCommands(int comport, List<string> commands)
        {
            var minipadValues = await GetMinipad(comport);
            if (minipadValues["state"] == "connected")
            {
                try
                {
                    // Open the serial port.
                    SerialPort serialPort = new SerialPort($"COM{comport}", 115200, Parity.Even, 8, StopBits.One);
                    serialPort.RtsEnable = true;
                    serialPort.DtrEnable = true;
                    serialPort.Open();

                    // Write command to the serial interface.
                    foreach(var command in commands)
                        serialPort.WriteLine(command);

                    // Safely close the serial port for usage by other processes.
                    serialPort.Close();
                }
                catch (Exception ex)
                {
                    // Failed to send command
                    return 2;
                }
                return 0;
            }
            else
            {
                // Busy/disconnected
                return 1;
            }
        }

        public static Tuple<int[], int[]> GetSensorValuesTuples(int keycount, int comport)
        {
            int[] rawValues = new int[keycount];
            int[] mappedValues = new int[keycount];
            for(int i = 0; i < keycount; i++)
            {
                rawValues[i] = -1;
                mappedValues[i] = -1;
            }

            SerialPort port = new SerialPort($"COM{comport}", 115200, Parity.None, 8, StopBits.One);
            port.RtsEnable = true;
            port.DtrEnable = true;
            port.DataReceived += (sender, e) =>
            {
                lock (port)
                {
                    // Read from the serial interface while data is available.
                    while (port.BytesToRead > 0)
                    {
                        // Read the current line and remove the \r at the end.
                        string line = port.ReadLine().Replace("\r", "");

                        // Check whether the line starts with "OUT" indicating the sensor output. ("OUT hkey?=rawValue mappedValue")
                        if (!line.StartsWith("OUT"))
                            continue;

                        // Parse the key index and the  sensor value sfrom the output received and remember it.
                        int keyIndex = int.Parse(line.Split('=')[0].Substring(8)) - 1;
                        rawValues[keyIndex] = int.Parse(line.Split('=')[1].Split(' ')[0]);
                        mappedValues[keyIndex] = int.Parse(line.Split('=')[1].Split(' ')[1]);
                    }
                }
            };

            // Open the port, send the out command, wait until no value in the array is -1 anymore and safely close it.
            port.Open();
            port.WriteLine("out");
            while (rawValues.Any(x => x == -1))
                ;
            lock (port)
                port.Close();

            // Return the read values.
            return Tuple.Create(rawValues, mappedValues);
        }
        public static SensorValues GetSensorValues(int keycount, int comport)
        {
            SensorValues values = new SensorValues(keycount);

            SerialPort port = new SerialPort($"COM{comport}", 115200, Parity.None, 8, StopBits.One);
            port.RtsEnable = true;
            port.DtrEnable = true;
            port.DataReceived += (sender, e) =>
            {
                lock (port)
                {
                    // Read from the serial interface while data is available.
                    while (port.BytesToRead > 0)
                    {
                        // Read the current line and remove the \r at the end.
                        string line = port.ReadLine().Replace("\r", "");

                        // Check whether the line starts with "OUT" indicating the sensor output. ("OUT hkey?=rawValue mappedValue")
                        if (!line.StartsWith("OUT"))
                            continue;

                        // Parse the key index and the  sensor value sfrom the output received and remember it.
                        int keyIndex = int.Parse(line.Split('=')[0].Substring(8)) - 1;
                        values.RawValues[keyIndex] = int.Parse(line.Split('=')[1].Split(' ')[0]);
                        values.MappedValues[keyIndex] = int.Parse(line.Split('=')[1].Split(' ')[1]);
                    }
                }
            };

            // Open the port, send the out command, wait until no value in the array is -1 anymore and safely close it.
            port.Open();
            port.WriteLine("out");
            while (values.RawValues.Any(x => x == -1))
                ;
            lock (port)
                port.Close();

            // Return the read values.
            return values;
        }
    }
}
