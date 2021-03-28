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

namespace ledartstudio
{
    internal static class Program
    {
        internal enum ExitCode { Success, DeviceNotFound, TransferFailed }

        [STAThread]
        private static int Main(string[] userArgs)
        {
            try
            {
                // Handle args:
                var args = new ArgHandler(userArgs);

                // Search for target device amongst connected usb hid devices:
                var hidManager = new HidManager();
                var isTargetFound = hidManager.FindDevice((short)args.UsbVendorId, (short)args.UsbProductId);
                if (!isTargetFound)
                {
                    Logger.Log("USB transfer failed.\n", Logger.LOG_INFO);
                    return (int)ExitCode.TransferFailed;
                }
                Console.WriteLine($"USB device found (VendorID {args.UsbVendorId}, ProductID {args.UsbProductId}).");

                // Send break packet:
                var breakPacket = new byte[args.UsbPacketByteSize];
                for (int i = 0; i < breakPacket.Length; i++) breakPacket[i] = 0xFF;
                Logger.Log("Break packet: ", Logger.LOG_DBG);
                if (!hidManager.SendBreakPacket(breakPacket))
                {
                    var e = new Exception($"USB transfer failed.");
                    e.Data.Add("ExitCode", (int)ExitCode.TransferFailed);
                    throw e;
                }

                // Transfer data packets:
                int packetIdx = 0;
                int packetSuccessCount = 0;
                int totalPackets = args.UsbPacketList.Count;
                foreach (var dataPacket in args.UsbPacketList)
                {
                    Logger.Log("Data packet " + ++packetIdx + ": ", Logger.LOG_DBG);
                    if (!hidManager.SendDataPacket(dataPacket))
                    {
                        Logger.Log("USB transfer failed.\n", Logger.LOG_INFO);
                        Logger.Log(packetSuccessCount + " out of " + totalPackets + " data-packets sent.\n", Logger.LOG_INFO);
                        return (int)ExitCode.TransferFailed;
                    }
                    packetSuccessCount++;
                }

                if (args.DownloadLightshow)
                {
                    Logger.Log("Break packet: ", Logger.LOG_DBG);
                    if (!hidManager.SendBreakPacket(breakPacket))
                    {
                        Logger.Log("USB transfer failed.\n", Logger.LOG_INFO);
                        return (int)ExitCode.TransferFailed;
                    }
                }

                Logger.Log("USB transfer succeeded: ", Logger.LOG_INFO);
                Logger.Log(packetSuccessCount + " out of " + totalPackets + " data-packets sent.\n", Logger.LOG_INFO);
                return (int)ExitCode.Success;
            }
            catch (Exception e)
            {
                Console.Write(e.Message);
                return e.Data["ExitCode"] != null ? (int)e.Data["ExitCode"] : 1;
            }
        }
    }
}
