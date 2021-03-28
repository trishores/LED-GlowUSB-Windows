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
 * Code in this file draws from various sources, including:
 * 1) https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/
 * 2) https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/content/_hid/
 * 3) https://docs.microsoft.com/en-us/windows/desktop/api/fileapi/
 * 4) USB COMPLETE by Jan Axelson
 * 5) https://github.com/madwizard-thomas/winusbnet
 */

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ledartstudio
{
    internal partial class HidManager
    {
        internal class NativeMethods
        {
            #region hid.dll

            [StructLayout(LayoutKind.Sequential)]
            internal struct HidAttributes
            {
                internal Int32 Size;
                internal short VendorID;
                internal short ProductID;
                internal short VersionNumber;
            }

            internal struct HidpCaps
            {
                internal short Usage;
                internal short UsagePage;
                internal short InputReportByteLength;
                internal short OutputReportByteLength;
                internal short FeatureReportByteLength;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] internal short[] Reserved;
                internal short NumberLinkCollectionNodes;
                internal short NumberInputButtonCaps;
                internal short NumberInputValueCaps;
                internal short NumberInputDataIndices;
                internal short NumberOutputButtonCaps;
                internal short NumberOutputValueCaps;
                internal short NumberOutputDataIndices;
                internal short NumberFeatureButtonCaps;
                internal short NumberFeatureValueCaps;
                internal short NumberFeatureDataIndices;
            }

            [DllImport("hid.dll", SetLastError = true)]
            internal static extern void HidD_GetHidGuid(ref Guid hidGuid);
            // https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/content/hidsdi/nf-hidsdi-hidd_gethidguid

            [DllImport("hid.dll", SetLastError = true)]
            internal static extern Byte HidD_GetAttributes(SafeFileHandle hidDeviceObject, ref HidAttributes attributes);
            // https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/content/hidsdi/nf-hidsdi-hidd_getattributes

            [DllImport("hid.dll", SetLastError = true)]
            internal static extern Int32 HidP_GetCaps(IntPtr preparsedData, ref HidpCaps capabilities);
            // https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/content/hidpi/nf-hidpi-hidp_getcaps

            [DllImport("hid.dll", SetLastError = true)]
            internal static extern Byte HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, ref IntPtr preparsedData);
            // https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/content/hidsdi/nf-hidsdi-hidd_getpreparseddata

            [DllImport("hid.dll", SetLastError = true)]
            internal static extern Byte HidD_FreePreparsedData(IntPtr preparsedData);
            // https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/content/hidsdi/nf-hidsdi-hidd_freepreparseddata

            [DllImport("hid.dll", SetLastError = true)]
            internal static extern Byte HidD_GetInputReport(SafeFileHandle hidDeviceObject, Byte[] lpReportBuffer, Int32 reportBufferLength);
            // https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/content/hidsdi/nf-hidsdi-hidd_getinputreport

            [DllImport("hid.dll", SetLastError = true)]
            internal static extern Byte HidD_SetOutputReport(SafeFileHandle hidDeviceObject, Byte[] lpReportBuffer, Int32 reportBufferLength);
            // https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/content/hidsdi/nf-hidsdi-hidd_setoutputreport

            [DllImport("hid.dll", SetLastError = true)]
            internal static extern Byte HidD_FlushQueue(SafeFileHandle hidDeviceObject);
            // https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/content/hidsdi/nf-hidsdi-hidd_flushqueue

            #endregion

            #region setupapi.dll

            internal const Int32 DIGCF_PRESENT = 2;
            internal const Int32 DIGCF_DEVICEINTERFACE = 0X10;
            internal const Int32 ERROR_NO_MORE_ITEMS = 259;

            internal struct SP_DEVICE_INTERFACE_DATA
            {
                internal Int32 CbSize;
                internal Guid InterfaceClassGuid;
                internal Int32 Flags;
                internal IntPtr Reserved;
            }
            // https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/ns-setupapi-_sp_device_interface_data

            //internal struct SP_DEVICE_INTERFACE_DETAIL_DATA
            //{
            //    internal Int32 CbSize;
            //    string DevicePath;
            //} 
            // https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/ns-setupapi-_sp_device_interface_detail_data_a

            [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
            internal static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, Int32 flags);
            // https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/nf-setupapi-setupdigetclassdevsw

            [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
            internal static extern Int32 SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
            // https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/nf-setupapi-setupdidestroydeviceinfolist

            [DllImport("setupapi.dll", SetLastError = true)]
            internal static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, Int32 memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);
            // https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/nf-setupapi-setupdienumdeviceinterfaces

            [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
            internal static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, Int32 deviceInterfaceDetailDataSize, ref Int32 requiredSize, IntPtr deviceInfoData);
            // https://docs.microsoft.com/en-us/windows/desktop/api/setupapi/ns-setupapi-_sp_device_interface_detail_data_a

            #endregion

            #region kernel32.dll

            internal const Int32 FileFlagOverlapped = 0X40000000;
            internal const Int32 FileShareRead = 1;
            internal const Int32 FileShareWrite = 2;
            internal const UInt32 GenericRead = 0X80000000;
            internal const UInt32 GenericWrite = 0X40000000;
            internal const Int32 OpenExisting = 3;

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern SafeFileHandle CreateFile(string lpFileName, UInt32 dwDesiredAccess, Int32 dwShareMode, IntPtr lpSecurityAttributes, Int32 dwCreationDisposition, Int32 dwFlagsAndAttributes, IntPtr hTemplateFile);
            // https://docs.microsoft.com/en-us/windows/desktop/api/fileapi/nf-fileapi-createfilea

            #endregion
        }
    }
} 
