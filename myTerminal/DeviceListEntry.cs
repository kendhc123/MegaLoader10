using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;

namespace myTerminal
{
	class DeviceListEntry
	{
		private DeviceInformation device;
		private String deviceSelector;
        private String portName;

		public String InstanceId
		{
			get
			{
				return device.Properties[DeviceProperties.DeviceInstanceId] as String;
			}
		}

		public DeviceInformation DeviceInformation
		{
			get
			{
				return device;
			}
		}

		public String DeviceName
		{
			get
			{
				return device.Name;
			}
		}

		public String DeviceSelector
		{
			get
			{
				return deviceSelector;
			}
		}

		/// <summary>
		/// The class is mainly used as a DeviceInformation wrapper so that the UI can bind to a list of these.
		/// </summary>
		/// <param name="deviceInformation"></param>
		/// <param name="deviceSelector">The AQS used to find this device</param>
		public DeviceListEntry(Windows.Devices.Enumeration.DeviceInformation deviceInformation, String deviceSelector)
		{
			device = deviceInformation;
			this.deviceSelector = deviceSelector;
		}
	}
}
