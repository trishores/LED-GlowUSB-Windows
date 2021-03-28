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
 * 
 * Code attribution:
 * 1) https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/
 * 2) https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/content/_hid/
 * 3) https://docs.microsoft.com/en-us/windows/desktop/api/fileapi/
 * 4) USB COMPLETE book by Jan Axelson
 * 5) https://github.com/madwizard-thomas/winusbnet
 */

using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace ledartstudio
{
    internal partial class HidManager
    {
        private SafeFileHandle _readHandle;
        private SafeFileHandle _writeHandle;
        private NativeMethods.HidpCaps _deviceCapabilities;
        private NativeMethods.HidAttributes _deviceAttributes;

        const int CONTROL_FLAG = 0;
        const int STORE_FLAG = 1;

        ~HidManager()
        {
            // Close any open handles:
            if (_readHandle != null && !_readHandle.IsInvalid) _readHandle.Close();
            if (_writeHandle != null && !_writeHandle.IsInvalid) _writeHandle.Close();
        }

        #region Device detection

        // Use vendor/product ids to find a hid device:.
        internal bool FindDevice(short targetVendorId, short targetProductId)
        {
            try
            {
                // Get hid class guid:
                var hidGuid = Guid.Empty;
                NativeMethods.HidD_GetHidGuid(ref hidGuid);

                // Get list of device paths for enumerated hid devices:
                var connectedDevicePaths = new string[64];
                if (!GetDevicePathsByGuid(hidGuid, ref connectedDevicePaths)) return false;
                
                foreach (var devicePath in connectedDevicePaths)
                {
                    // Get read/write hndle to target device: 
                    var targetDeviceHandle = NativeMethods.CreateFile(devicePath, 0, NativeMethods.FileShareRead | NativeMethods.FileShareWrite, IntPtr.Zero, NativeMethods.OpenExisting, 0, IntPtr.Zero);
                    if (targetDeviceHandle.IsInvalid) continue;

                    // Check whether vendor/product ids are a match:
                    if (!GetDeviceAttributes(targetDeviceHandle, ref _deviceAttributes) ||
                        _deviceAttributes.VendorID != targetVendorId || 
                        _deviceAttributes.ProductID != targetProductId)
                    {
                        targetDeviceHandle.Close();
                        continue;
                    }

                    // Target device found.

                    // Get read handle:
                    _readHandle = NativeMethods.CreateFile(devicePath, NativeMethods.GenericRead, NativeMethods.FileShareRead | NativeMethods.FileShareWrite, IntPtr.Zero, NativeMethods.OpenExisting, NativeMethods.FileFlagOverlapped, IntPtr.Zero);
                    if (_readHandle.IsInvalid)
                    {
                        Console.WriteLine("Invalid device read handle.");
                        return false;
                    }

                    // Get write handle:
                    _writeHandle = NativeMethods.CreateFile(devicePath, NativeMethods.GenericWrite, NativeMethods.FileShareRead | NativeMethods.FileShareWrite, IntPtr.Zero, NativeMethods.OpenExisting, 0, IntPtr.Zero);
                    if (_writeHandle.IsInvalid)
                    {
                        Console.WriteLine("Invalid device write handle.");
                        return false;
                    }

                    // Get device capabilities:
                    if (!GetDeviceCapabilities(targetDeviceHandle, ref _deviceCapabilities))
                    {
                        Console.WriteLine("Unable to get device capabilities.");
                        return false;
                    }

                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        // Get connected device paths:
        private static bool GetDevicePathsByGuid(Guid hidClassGuid, ref string[] connectedDevicePaths)
        {
            var diDetailDataSize = 0;
            var deviceIndex = 0;
            var ptrDeviceInfoSet = IntPtr.Zero;

            try
            {
                // Get handle to device information set for enumerated hid devices:
                ptrDeviceInfoSet = NativeMethods.SetupDiGetClassDevs(ref hidClassGuid, IntPtr.Zero, IntPtr.Zero, NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);

                // Instantiate an empty DEVICE_INTERFACE_DATA structure and set its size field:
                var diData = new NativeMethods.SP_DEVICE_INTERFACE_DATA();
                diData.CbSize = Marshal.SizeOf(diData);

                while (true)
                {
                    // Populates DEVICE_INTERFACE_DATA structure:
                    var isSuccess = NativeMethods.SetupDiEnumDeviceInterfaces(ptrDeviceInfoSet, IntPtr.Zero, ref hidClassGuid, deviceIndex, ref diData);
                    if (!isSuccess && Marshal.GetLastWin32Error() == NativeMethods.ERROR_NO_MORE_ITEMS) break;  // end of enumerated hid devices.

                    // Call first time to retrieve size of DEVICE_INTERFACE_DETAIL_DATA structure:
                    NativeMethods.SetupDiGetDeviceInterfaceDetail(ptrDeviceInfoSet, ref diData, IntPtr.Zero, 0, ref diDetailDataSize, IntPtr.Zero);

                    // Instantiate an empty DEVICE_INTERFACE_DETAIL_DATA structure and set its size field:
                    var diDetailData = Marshal.AllocHGlobal(diDetailDataSize);
                    Marshal.WriteInt32(diDetailData, (IntPtr.Size == 4 ? Marshal.SystemDefaultCharSize + 4 : 8));

                    // Call second time to retrieve populated DEVICE_INTERFACE_DETAIL_DATA structure:
                    NativeMethods.SetupDiGetDeviceInterfaceDetail(ptrDeviceInfoSet, ref diData, diDetailData, diDetailDataSize, ref diDetailDataSize, IntPtr.Zero);

                    // Get device path from DEVICE_INTERFACE_DETAIL_DATA structure:
                    var pDevicePath = new IntPtr(diDetailData.ToInt32() + 4);
                    connectedDevicePaths[deviceIndex] = Marshal.PtrToStringAuto(pDevicePath);

                    // Free memory previously allocated by AllocHGlobal:
                    if (diDetailData != IntPtr.Zero) Marshal.FreeHGlobal(diDetailData);

                    deviceIndex++;
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
            finally
            {
                //  Free memory reserved for device information set by SetupDiGetClassDevs:
                if (ptrDeviceInfoSet != IntPtr.Zero)
                {
                    NativeMethods.SetupDiDestroyDeviceInfoList(ptrDeviceInfoSet);
                }
            }
        }

        #endregion

        #region Write output report

        internal bool SendBreakPacket(byte[] breakPacket)
        {
            var hidOutputReport = new List<byte> { 0 }; // first byte is report id (zero).
            hidOutputReport.AddRange(breakPacket);

            // Write output report to device:
            if (!WriteOutputReport(hidOutputReport.ToArray()))
            {
                Logger.Log("write failed unexpectedly.\n", Logger.LOG_DBG);
                return false;
            }

            var retryCount = 10;
            while (retryCount-- > 0)
            {
                // Get report from device:
                var res = ReadInputReport(out byte[] responsePacket);
                var isActiveAnimation = responsePacket[1] != 0;
                var isActiveMemWrite = responsePacket[2] != 0;
                var isControlMode = responsePacket[3] == CONTROL_FLAG;

                if (res && !isActiveAnimation && !isActiveMemWrite && isControlMode)
                {
                    Logger.Log("sent successfully.\n", Logger.LOG_DBG);
                    return true;
                }
                else if (res && isActiveAnimation)
                {
                    Logger.Log("animation in progress... recheck in 100ms... ", Logger.LOG_DBG);
                    Thread.Sleep(100);
                    continue;
                }
                else if (res && isActiveMemWrite)
                {
                    Logger.Log("write in progress... recheck in 100ms... ", Logger.LOG_DBG);
                    Thread.Sleep(100);
                    continue;
                }
                else
                {
                    Logger.Log("read failed unexpectedly.\n", Logger.LOG_DBG);
                    return false;
                }
            }
            return false;
        }

        internal bool SendDataPacket(byte[] dataPacket)
        {
            var hidOutputReport = new List<byte> { 0 }; // first byte is report id.
            hidOutputReport.AddRange(dataPacket);

            // Write output report to device:
            if (!WriteOutputReport(hidOutputReport.ToArray()))
            {
                Logger.Log("write failed unexpectedly.\n", Logger.LOG_DBG);
                return false;
            }

            var retryCount = 10;
            while (retryCount-- > 0)
            {
                // Get report from device:                
                var res = ReadInputReport(out byte[] responsePacket);
                bool isActiveMemWrite = responsePacket[2] != 0;
                if (res && !isActiveMemWrite)
                {
                    Logger.Log("sent successfully.\n", Logger.LOG_DBG);
                    return true;
                }
                else if (res && isActiveMemWrite)
                {
                    Logger.Log("write in progress... recheck in 100ms... ", Logger.LOG_DBG);
                    Thread.Sleep(100);
                    continue;
                }
                else
                {
                    Logger.Log("read failed unexpectedly.\n", Logger.LOG_DBG);
                    return false;
                }
            }
            return false;
        }

        private bool WriteOutputReport(byte[] hidOutputReport)
        {
            try
            {
                if (_writeHandle.IsInvalid)
                {
                    Console.WriteLine("Invalid device handle.");
                    return false;
                }
                
                if (_deviceCapabilities.OutputReportByteLength != hidOutputReport.Length)
                {
                    Console.WriteLine("Unexpected output report length.");
                    return false;
                }

                // Write output report via control transfer:
                if (NativeMethods.HidD_SetOutputReport(_writeHandle, hidOutputReport, hidOutputReport.Length + 1) == 0)
                {
                    Console.WriteLine("Failed to write output report.");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        #endregion

        #region Read input report

        internal bool ReadInputReport(out byte[] responsePacket)
        {
            responsePacket = null;

            try
            {
                if (_readHandle.IsInvalid)
                {
                    Console.WriteLine("Invalid device handle (system mouse/keyboard?).");
                    return false;
                }

                if (_deviceCapabilities.InputReportByteLength == 0)
                {
                    Console.WriteLine("Input report unsupported.");
                    return false;
                }

                // Set the size of the Input report buffer. 
                var inputReportBuffer = new byte[_deviceCapabilities.InputReportByteLength];

                // Read input report via control transfer:
                var len = NativeMethods.HidD_GetInputReport(_readHandle, inputReportBuffer, inputReportBuffer.Length + 1);
                if (NativeMethods.HidD_GetInputReport(_readHandle, inputReportBuffer, inputReportBuffer.Length + 1) == 0)
                {
                    Console.WriteLine("Failed to read input report.");
                    return false;
                }

                // Parse input report:
                responsePacket = inputReportBuffer;
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        #endregion

        #region Device capabilities

        private static bool GetDeviceCapabilities(SafeFileHandle hidHandle, ref NativeMethods.HidpCaps deviceCapabilities)
        {
            var preparsedData = new IntPtr();

            try
            {
                NativeMethods.HidD_GetPreparsedData(hidHandle, ref preparsedData);
                NativeMethods.HidP_GetCaps(preparsedData, ref deviceCapabilities);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
            finally
            {
                if (preparsedData != IntPtr.Zero) NativeMethods.HidD_FreePreparsedData(preparsedData);
            }
        }

        private bool GetDeviceAttributes(SafeFileHandle hidHandle, ref NativeMethods.HidAttributes deviceAttributes)
        {
            // Set attribute structure size field:
            _deviceAttributes.Size = Marshal.SizeOf(_deviceAttributes);

            try
            {
                // Get populated attribute structure containing vendor/product ids:
                var isSuccess = (NativeMethods.HidD_GetAttributes(hidHandle, ref deviceAttributes) > 0);
                return isSuccess;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

            #endregion
    }
}
