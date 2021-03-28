/* 
 * MIT License
 * 
 * Copyright 2018-2021 ledmaker.org
 * 
 * This file is part of GlowUSB-Windows.
 *  
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use, copy,
 * modify, merge, publish, distribute, sublicense, and/or sell copies
 * of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ledartstudio
{
    internal class ArgHandler
    {
        private enum Switch
        {
            // Mandatory switches:
            InputFilePath,
            UsbVendorId,
            UsbProductId,
            UsbPacketByteSize,
            UsbPacketList,
            Download, // command to download to usb device.
            Start, // command to start an downloaded lightshow.
            Stop, // command to pause the current lightshow.
            Resume // command to resume a stopped lightshow.
        };
        private enum ExitCode
        {
            Success,
            InvalidArgs,
            InvalidFileInputPath,
            InvalidUsbExecFileInputPath,
            InvalidFormat,
            DeviceNotFound
        }

        internal short UsbVendorId;
        internal short UsbProductId;
        internal short UsbPacketByteSize;
        internal List<byte[]> UsbPacketList = new List<byte[]>();
        internal string _inputFilePath;
        internal bool DownloadLightshow, StartLightshow, PauseLightshow, ResumeLightshow;
        private List<string> _switchArgs = new List<string>();

        internal ArgHandler(string[] args)
        {
            Parse(args);
        }

        internal void Parse(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("One or more args expected.");
                Environment.Exit((int)ExitCode.InvalidArgs);
            }
            
            // Build list of accepted switches:
            foreach (var switchArg in Enum.GetValues(typeof(Switch)))
            {
                _switchArgs.Add($"-{Enum.GetName(typeof(Switch), switchArg)}");
            }

            string prevSwitchArg = null;
            foreach (var currArg in args)
            {
                if (currArg.StartsWith("-"))
                {
                    if (!_switchArgs.ToList().Any(x => x.Equals(currArg, StringComparison.OrdinalIgnoreCase)))
                    {
                        Console.WriteLine($"Unrecognized switch '{currArg}'.");
                        Environment.Exit((int)ExitCode.InvalidArgs);
                    }

                    prevSwitchArg = null;

                    if (currArg.Equals(_switchArgs[(int)Switch.Download], StringComparison.OrdinalIgnoreCase))
                    {
                        DownloadLightshow = true;
                    }
                    else if (currArg.Equals(_switchArgs[(int)Switch.Start], StringComparison.OrdinalIgnoreCase))
                    {
                        StartLightshow = true;
                    }
                    if (currArg.Equals(_switchArgs[(int)Switch.Stop], StringComparison.OrdinalIgnoreCase))
                    {
                        PauseLightshow = true;
                    }
                    else if (currArg.Equals(_switchArgs[(int)Switch.Resume], StringComparison.OrdinalIgnoreCase))
                    {
                        ResumeLightshow = true;
                    }
                    else
                    {
                        prevSwitchArg = currArg;
                    }

                    continue;
                }
                else if (!currArg.StartsWith("-") && prevSwitchArg == null)
                {
                    Console.WriteLine($"Unrecognized arg '{currArg}'.");
                    Environment.Exit((int)ExitCode.InvalidArgs);
                }
                else if (!currArg.StartsWith("-") && prevSwitchArg != null)
                {
                    if (prevSwitchArg.Equals(_switchArgs[(int)Switch.InputFilePath], StringComparison.OrdinalIgnoreCase))
                    {
                        _inputFilePath = currArg;
                    }
                    else if (prevSwitchArg.Equals(_switchArgs[(int)Switch.UsbVendorId], StringComparison.OrdinalIgnoreCase))
                    {
                        UsbVendorId = short.Parse(currArg);
                    }
                    else if (prevSwitchArg.Equals(_switchArgs[(int)Switch.UsbProductId], StringComparison.OrdinalIgnoreCase))
                    {
                        UsbProductId = short.Parse(currArg);
                    }
                    else if (prevSwitchArg.Equals(_switchArgs[(int)Switch.UsbPacketByteSize], StringComparison.OrdinalIgnoreCase))
                    {
                        UsbPacketByteSize = short.Parse(currArg);
                    }
                    else if (prevSwitchArg.Equals(_switchArgs[(int)Switch.UsbPacketList], StringComparison.OrdinalIgnoreCase))
                    {
                        UsbPacketList = ParsePackets(currArg);
                    }
                    else
                    {
                        Console.WriteLine($"Unrecognized switch arg '{prevSwitchArg}'.");
                        Environment.Exit((int)ExitCode.InvalidArgs);
                    }

                    prevSwitchArg = null;
                    continue;
                }
            }

            if (_inputFilePath != null && !File.Exists(_inputFilePath))
            {
                Console.WriteLine("Invalid input file path.");
                Environment.Exit((int)ExitCode.InvalidFileInputPath);
            }

            if (_inputFilePath != null)
            {
                // Read xml file containing usb data:
                try
                {
                    // Parse usb data:
                    var xdoc = XDocument.Load(_inputFilePath);
                    UsbVendorId = short.Parse(xdoc.Descendants("usbVendorId").SingleOrDefault().Value);
                    UsbProductId = short.Parse(xdoc.Descendants("usbProductId").SingleOrDefault().Value);
                    UsbPacketByteSize = short.Parse(xdoc.Descendants("usbPacketByteLen").SingleOrDefault().Value);
                    if (DownloadLightshow)
                    {
                        UsbPacketList = ParsePackets(xdoc.Descendants("downloadLightshowPackets").SingleOrDefault().Value);
                    }
                    if (StartLightshow)
                    {
                        UsbPacketList = ParsePackets(xdoc.Descendants("startLightshowPackets").SingleOrDefault().Value);
                    }
                    if (PauseLightshow)
                    {
                        UsbPacketList = ParsePackets(xdoc.Descendants("pauseLightshowPackets").SingleOrDefault().Value);
                    }
                    if (ResumeLightshow)
                    {
                        UsbPacketList = ParsePackets(xdoc.Descendants("resumeLightshowPackets").SingleOrDefault().Value);
                    }
                }
                catch
                {
                    Console.WriteLine("Error parsing xml input file");
                    Environment.Exit((int)ExitCode.InvalidFormat);
                }
            }
        }

        private List<byte[]> ParsePackets(string bytesStr)
        {
            var packetList = new List<byte[]>();
            var separators = (new[] { ',', ' ' }).Concat(Environment.NewLine.ToCharArray()).ToArray();
            var byteStrArray = bytesStr.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            var byteArray = byteStrArray.Select(hexStr => Convert.ToByte(hexStr, 16)).ToArray();
            var packet = new List<byte>();
            for (var i = 0; i < byteArray.Length; i++)
            {
                packet.Add(byteArray[i]);
                if ((i + 1) % UsbPacketByteSize == 0)
                {
                    packetList.Add(packet.ToArray());
                    packet.Clear();
                }
            }
            return packetList;
        }
    }
}
