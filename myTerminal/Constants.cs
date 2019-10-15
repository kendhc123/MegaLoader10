using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace myTerminal
{
	public class DeviceProperties
	{
		public const String DeviceInstanceId = "System.Devices.DeviceInstanceId";
	}

	public class DeviceConfiguration
	{
		public const UInt32 BaudRate = 115200;

		public const Windows.Devices.SerialCommunication.SerialParity Parity = Windows.Devices.SerialCommunication.SerialParity.Even;

		public const Windows.Devices.SerialCommunication.SerialStopBitCount StopBits = Windows.Devices.SerialCommunication.SerialStopBitCount.OnePointFive;

		public const UInt16 DataBits = 8;

		public const Windows.Devices.SerialCommunication.SerialHandshake Handshake = Windows.Devices.SerialCommunication.SerialHandshake.RequestToSend;

		public const Boolean BreakSignalState_false = false;
		public const Boolean BreakSignalState_true = true;

		public const Boolean IsDataTerminalReady_false = false;
		public const Boolean IsDataTerminalReady_true = true;

		public const Boolean IsRequestToSendEnabled_false = false;
		public const Boolean IsRequestToSendEnabled_true = true;

	}
}
