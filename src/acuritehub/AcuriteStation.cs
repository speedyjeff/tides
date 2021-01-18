using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using Acurite;

// https://github.com/pyusb/pyusb/blob/a16251f3d62de1e0b50cdfb431482d08a34355b4/usb/legacy.py
// https://github.com/weewx/weewx/blob/5fbe0d51e88cfb126543e5309a970be220e7dcc0/bin/weewx/drivers/acurite.py#L656

namespace acuritehub
{
    public class AcuriteStation
    {
        public AcuriteStation()
        {
            Context = new UsbContext();
            HasConfigured = new HashSet<long>();
        }

        public AcuriteData Read()
        {
            return Read(VendorId, ProductId);
        }

        public AcuriteData Read(UsbDeviceId id)
        {
            return Read(id.VendorId, id.ProductId);
        }

        public AcuriteData Read(int vendorid, int productid)
        {
            var result = new AcuriteData() { utcDate = DateTime.UtcNow };
            IUsbDevice wholeUsbDevice;

            // find the device
            var device = Context.Find((usb) => usb.ProductId == productid && usb.VendorId == vendorid);

            if (device == null) throw new Exception("Failed to access the weather station");

            try
            {
                // open the device for communication
                device.Open();

                // on Linux we need to ask the kernel to detach the interface first
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                {
                    var key = ((long)productid << 32) | ((long)vendorid);
		    if (!HasConfigured.Contains(key))
                    {
                        if (device.DeviceHandle == null) throw new Exception("failed to properly open device");
                        var ec = DetachKernelDriver(device.DeviceHandle, 0);
                        if (ec != Error.Success) Console.WriteLine($"failed to detach kernel from device : {ec}");
                        HasConfigured.Add(key);
                    }
                }

                // If this is a "whole" usb device (libusb-win32, linux libusb-1.0)
                // it exposes an IUsbDevice interface. If not (WinUSB) the 
                // 'wholeUsbDevice' variable will be null indicating this is 
                // an interface of a device; it does not require or support 
                // configuration and interface selection.
                wholeUsbDevice = device as IUsbDevice;
                if (!ReferenceEquals(wholeUsbDevice, null))
                {
                    // This is a "whole" USB device. Before it can be used, 
                    // the desired configuration and interface must be selected.

                    // Select config #1
                    wholeUsbDevice.SetConfiguration(1);

                    // Claim interface #0.
                    wholeUsbDevice.ClaimInterface(0);
                }

                // read reports
                if (!ReadReport1(device, ref result)) throw new Exception("Failed to access report 1");
                if (!ReadReport2(device, ref result)) throw new Exception("Failed to access report 2");
            }
            finally
            {
                if (device.IsOpen)
                {
                    // If this is a "whole" usb device (libusb-win32, linux libusb-1.0)
                    // it exposes an IUsbDevice interface. If not (WinUSB) the 
                    // 'wholeUsbDevice' variable will be null indicating this is 
                    // an interface of a device; it does not require or support 
                    // configuration and interface selection.
                    wholeUsbDevice = device as IUsbDevice;
                    if (!ReferenceEquals(wholeUsbDevice, null))
                    {
                        // Release interface #0.
                        wholeUsbDevice.ReleaseInterface(0);
                    }

                    device.Close();
                }
            }

            return result;
        }

        public static List<UsbDeviceId> All()
        {
            var result = new List<UsbDeviceId>();
            using (var context = new UsbContext())
            {
                foreach (var d in context.List())
                {
                    result.Add(new UsbDeviceId() { ProductId = d.ProductId, VendorId = d.VendorId });
                }
            }

            return result;
        }

        #region private
        // AcuRite 6004
        private const int VendorId = 0x24c0;
        private const int ProductId = 0x0003;

        private const short USB_HID_INPUT_REPORT = 0x0100;
        private const byte USB_HID_GET_REPORT = 0x01;

        private UsbContext Context;

        // wind direction index to degrees on a compass
        private readonly float[] WindDirection = new float[]
        {
            315.0f, // 0
            247.5f, // 1
            292.5f, // 2
            270.0f, // 3
            337.5f, // 4
            225.0f, // 5
            0.0f, // 6
            202.5f, // 7
            67.5f, // 8
            135.0f, // 9
            90.0f, // 10
            112.5f, // 11
            45.0f, // 12
            157.5f, // 13
            22.5f, // 14
            180.0f, // 15
        };

	private HashSet<long> HasConfigured;

        // necessary to ensure the kernel is not using the usb interface
        [DllImport("libusb-1.0.so.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "libusb_detach_kernel_driver")]
        private static extern Error DetachKernelDriver(DeviceHandle devHandle, int interfaceNumber);

        private bool ReadReport1(IUsbDevice device, ref AcuriteData result)
        {
            var buffer = new byte[10];

            if (!Read(device, report: 1, buffer)) throw new Exception("Failed to read data");

            // ensure valid
            if (buffer.Length != 10 || buffer[0] != 0x01) throw new Exception("Invalid report 1 data");

            // check for errors
            if ((buffer[1] & 0x0f) == 0x0f && buffer[3] == 0xff)
            {
                // no sensor data
                return false;
            }
            else if ((buffer[3] & 0x0f) != 1 && (buffer[3] & 0x0f) != 8)
            {
                // bogus message flavor
                return false;
            }
            else if (buffer[9] != 0xff && buffer[9] != 0x0)
            {
                // bogus final byte
                return false;
            }
            else if ((buffer[8] & 0x0f) < 0 || (buffer[8] & 0x0f) > 3)
            {
                // bogus signal strength
                return false;
            }

            // decode

            // channel (todo A B C)
            result.channel = (ChannelName)(buffer[1] & 0xf0);

            // sensor id
            result.sensorId = ((buffer[1] & 0x0f) << 8) | buffer[2];

            // signal strength
            result.signal = (SignalStrength)(buffer[8] & 0x0f);

            // stale data
            if (result.signal == SignalStrength.None) return false;

            // low battery
            result.lowBattery = ((buffer[3] & 0xf0) >> 4) != 0x7;

            // wind speed
            result.windSpeed = (float)(((buffer[4] & 0x1f) << 3) | ((buffer[5] & 0x70) >> 4));
            if (result.windSpeed > 0) result.windSpeed = (result.windSpeed * 0.8278f) + 1.0f;
            result.windSpeed *= 0.62137f; // mph

            if ((buffer[3] & 0xf) == 1)
            {
                // wind direction
                result.windDirection = WindDirection[buffer[5] & 0x0f]; // degrees

                // rain total
                result.rainTotal = (((buffer[6] & 0x3f) << 7) | (buffer[7] & 0x7f)) / 100f; // inches
            }
            else
            {
                // outside temperature
                result.outTemperature = (float)(((buffer[5] & 0x0f) << 7) | (buffer[6] & 0x7f)) / 18.0f - 40.0f;
                result.outTemperature = result.outTemperature * 1.8f + 32f; // fahrenheit

                // outside humidity
                result.outHumidity = buffer[7] & 0x7f; // percentage
            }

            return true;
        }

        private bool ReadReport2(IUsbDevice device, ref AcuriteData result)
        {
            var buffer = new byte[25];

            if (!Read(device, report: 2, buffer)) throw new Exception("Failed to read data");

            // ensure valid
            if (buffer.Length != 25 || buffer[0] != 0x02) throw new Exception("Invalid report 2 data");

            // constants
            var c1 = ((buffer[3] << 8) + buffer[4]);
            var c2 = ((buffer[5] << 8) + buffer[6]);
            var c3 = ((buffer[7] << 8) + buffer[8]);
            var c4 = ((buffer[9] << 8) + buffer[10]);
            var c5 = ((buffer[11] << 8) + buffer[12]);
            var c6 = ((buffer[13] << 8) + buffer[14]);
            var c7 = ((buffer[15] << 8) + buffer[16]);
            var a = buffer[17];
            var b = buffer[18];
            var c = buffer[19];
            var d = buffer[20];

            if (c1 == 0x8000 && c2 == c3 && c3 == 0x0 &&
                c4 == 0x0400 && c5 == 0x1000 && c6 == 0x0 &&
                c7 == 0x0960 && a == b && b == c && c == d && d == 0x1)
            {
                // MS5607 sensor, typical in 02032 consoles
                var d2 = (float)(((buffer[21] & 0x0f) << 8) + buffer[22]);
                if (d2 >= 0x0800) d2 -= 0x1000;
                var d1 = (float)((buffer[23] << 8) + buffer[24]);

                // decode

                // pressure
                result.pressure = (d1 / 16.0f) - (208f /* + 250f hack */); // mbar
                result.pressure /= 33.863886667f; // inHg

                // inside temperature
                result.inTemperature = 25.0f + (0.05f * d2);
                result.inTemperature = (result.inTemperature * 1.8f) + 32f; // fahrenheit
            }
            else
            {
                return false;
            }

            return true;
        }

        private bool Read(IUsbDevice device, int report, byte[] buffer)
        {
            // setup
            var setup = new UsbSetupPacket(
                (byte)UsbCtrlFlags.Direction_In | (byte)UsbCtrlFlags.RequestType_Class | (byte)UsbCtrlFlags.Recipient_Interface, // type
                USB_HID_GET_REPORT, // request
                (short)(USB_HID_INPUT_REPORT | report), // wValue
                0, // windex
                buffer.Length // wlength
                );

            // request
            var tries = 3;
            do
            {
                try
                {
                    // request control transfer
                    var ret = device.ControlTransfer(setup, buffer, offset: 0, buffer.Length);
                    return ret == buffer.Length;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{DateTime.Now:o}: Reading from device failed {e.Message}");
                }

                System.Threading.Thread.Sleep(500);
            }
            while (--tries > 0);

            return false;
        }
        #endregion
    }
}
