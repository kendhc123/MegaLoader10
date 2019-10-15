using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using System.Collections.ObjectModel;
using Windows.UI.Core;
using Windows.ApplicationModel;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI;
using System.Threading;
using Windows.Storage.Streams;
using System.Threading.Tasks;

// 빈 페이지 항목 템플릿에 대한 설명은 https://go.microsoft.com/fwlink/?LinkId=234238에 나와 있습니다.

namespace myTerminal
{
	/// <summary>
	/// 자체적으로 사용하거나 프레임 내에서 탐색할 수 있는 빈 페이지입니다.
	/// </summary>

	public sealed partial class COMPage : Page
	{
		public static COMPage Current;

		private const String ButtonNameDisconnectFromDevice = "Disconnect from device";
		private const String ButtonNameDisableReconnectToDevice = "Do not automatically reconnect to device that was just closed";

		private MainPage rootPage = MainPage.Current;

		private SuspendingEventHandler appSuspendEventHandler;
		private EventHandler<Object> appResumeEventHandler;

		private ObservableCollection<DeviceListEntry> listOfDevices;

		private Dictionary<DeviceWatcher, String> mapDeviceWatchersToDeviceSelector;
		private Boolean watchersSuspended;
		private Boolean watchersStarted;

		// Has all the devices enumerated by the device watcher?
		private Boolean isAllDevicesEnumerated;

        //private ObservableCollection<DeviceListEntry> serialList = new ObservableCollection<DeviceListEntry>();
        /* Serial Read/Write member */
        // Track Read Operation
        private CancellationTokenSource ReadCancellationTokenSource;
        private Object ReadCancelLock = new Object();

        private Boolean IsReadTaskPending;
        private uint ReadBytesCounter = 0;
        DataReader DataReaderObject = null;

        // Track Write Operation
        private CancellationTokenSource WriteCancellationTokenSource;
        private Object WriteCancelLock = new Object();

        private Boolean IsWriteTaskPending;
        private uint WriteBytesCounter = 0;
        DataWriter DataWriteObject = null;

        bool WriteBytesAvailable = false;

        // Indicate if we navigate away from this page or not.
        private Boolean IsNavigatedAway;

        public COMPage()
		{
			this.InitializeComponent();
			listOfDevices = new ObservableCollection<DeviceListEntry>();

			mapDeviceWatchersToDeviceSelector = new Dictionary<DeviceWatcher, String>();
			watchersStarted = false;
			watchersSuspended = false;

			isAllDevicesEnumerated = false;
			Current = this;

		}
		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			if (EventHandlerForDevice.Current.IsDeviceConnected
				|| (EventHandlerForDevice.Current.IsEnabledAutoReconnect
				&& EventHandlerForDevice.Current.DeviceInformation != null))
			{
				UpdateConnectDisconnectButtonsAndList(false);

				// These notifications will occur if we are waiting to reconnect to device when we start the page
				EventHandlerForDevice.Current.OnDeviceConnected = this.OnDeviceConnected;
				EventHandlerForDevice.Current.OnDeviceClose = this.OnDeviceClosing;
			}
			else
			{
				UpdateConnectDisconnectButtonsAndList(true);
			}

			// Begin watching out for events
			StartHandlingAppEvents();

			// Initialize the desired device watchers so that we can watch for when devices are connected/removed
			InitializeDeviceWatchers();
			StartDeviceWatchers();

			serialList.Source = listOfDevices;
		}

		protected override void OnNavigatedFrom(NavigationEventArgs eventArgs)
		{
			StopDeviceWatchers();
			StopHandlingAppEvents();

			// We no longer care about the device being connected
			EventHandlerForDevice.Current.OnDeviceConnected = null;
			EventHandlerForDevice.Current.OnDeviceClose = null;
		}

		private void StartHandlingAppEvents()
		{
			appSuspendEventHandler = new SuspendingEventHandler(this.OnAppSuspension);
			appResumeEventHandler = new EventHandler<Object>(this.OnAppResume);

			// This event is raised when the app is exited and when the app is suspended
			App.Current.Suspending += appSuspendEventHandler;

			App.Current.Resuming += appResumeEventHandler;
		}

		private void StopHandlingAppEvents()
		{
			// This event is raised when the app is exited and when the app is suspended
			App.Current.Suspending -= appSuspendEventHandler;

			App.Current.Resuming -= appResumeEventHandler;
		}
		private void InitializeDeviceWatchers()
		{

			// Target all Serial Devices present on the system
			var deviceSelector = SerialDevice.GetDeviceSelector();

			// Other variations of GetDeviceSelector() usage are commented for reference
			//
			// Target a specific USB Serial Device using its VID and PID (here Arduino VID/PID is used)
			// var deviceSelector = SerialDevice.GetDeviceSelectorFromUsbVidPid(0x2341, 0x0043);
			//
			// Target a specific Serial Device by its COM PORT Name - "COM3"
			// var deviceSelector = SerialDevice.GetDeviceSelector("COM3");
			//
			// Target a specific UART based Serial Device by its COM PORT Name (usually defined in ACPI) - "UART1"
			// var deviceSelector = SerialDevice.GetDeviceSelector("UART1");
			//

			// Create a device watcher to look for instances of the Serial Device that match the device selector
			// used earlier.

			var deviceWatcher = DeviceInformation.CreateWatcher(deviceSelector);

			// Allow the EventHandlerForDevice to handle device watcher events that relates or effects our device (i.e. device removal, addition, app suspension/resume)
			AddDeviceWatcher(deviceWatcher, deviceSelector);
		}

		/// <summary>
		/// Registers for Added, Removed, and Enumerated events on the provided deviceWatcher before adding it to an internal list.
		/// </summary>
		/// <param name="deviceWatcher"></param>
		/// <param name="deviceSelector">The AQS used to create the device watcher</param>
		private void AddDeviceWatcher(DeviceWatcher deviceWatcher, String deviceSelector)
		{
			deviceWatcher.Added += new TypedEventHandler<DeviceWatcher, DeviceInformation>(this.OnDeviceAdded);
			deviceWatcher.Removed += new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>(this.OnDeviceRemoved);
			deviceWatcher.EnumerationCompleted += new TypedEventHandler<DeviceWatcher, Object>(this.OnDeviceEnumerationComplete);

			mapDeviceWatchersToDeviceSelector.Add(deviceWatcher, deviceSelector);
		}

		/// <summary>
		/// Starts all device watchers including ones that have been individually stopped.
		/// </summary>
		private void StartDeviceWatchers()
		{
			// Start all device watchers
			watchersStarted = true;
			isAllDevicesEnumerated = false;

			foreach (DeviceWatcher deviceWatcher in mapDeviceWatchersToDeviceSelector.Keys)
			{
				if ((deviceWatcher.Status != DeviceWatcherStatus.Started)
					&& (deviceWatcher.Status != DeviceWatcherStatus.EnumerationCompleted))
				{
					deviceWatcher.Start();
				}
			}
		}

		/// <summary>
		/// Stops all device watchers.
		/// </summary>
		private void StopDeviceWatchers()
		{
			// Stop all device watchers
			foreach (DeviceWatcher deviceWatcher in mapDeviceWatchersToDeviceSelector.Keys)
			{
				if ((deviceWatcher.Status == DeviceWatcherStatus.Started)
					|| (deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted))
				{
					deviceWatcher.Stop();
				}
			}

			// Clear the list of devices so we don't have potentially disconnected devices around
			ClearDeviceEntries();

			watchersStarted = false;
		}

		/// <summary>
		/// Creates a DeviceListEntry for a device and adds it to the list of devices in the UI
		/// </summary>
		/// <param name="deviceInformation">DeviceInformation on the device to be added to the list</param>
		/// <param name="deviceSelector">The AQS used to find this device</param>
		private void AddDeviceToList(DeviceInformation deviceInformation, String deviceSelector)
		{
			// search the device list for a device with a matching interface ID
			var match = FindDevice(deviceInformation.Id);

			// Add the device if it's new
			if (match == null)
			{
				// Create a new element for this device interface, and queue up the query of its
				// device information
				match = new DeviceListEntry(deviceInformation, deviceSelector);

				// Add the new element to the end of the list of devices
				listOfDevices.Add(match);
			}
		}

		private void RemoveDeviceFromList(String deviceId)
		{
			// Removes the device entry from the interal list; therefore the UI
			var deviceEntry = FindDevice(deviceId);

			listOfDevices.Remove(deviceEntry);
		}

		private void ClearDeviceEntries()
		{
			listOfDevices.Clear();
		}

		/// <summary>
		/// Searches through the existing list of devices for the first DeviceListEntry that has
		/// the specified device Id.
		/// </summary>
		/// <param name="deviceId">Id of the device that is being searched for</param>
		/// <returns>DeviceListEntry that has the provided Id; else a nullptr</returns>
		private DeviceListEntry FindDevice(String deviceId)
		{
			if (deviceId != null)
			{
				foreach (DeviceListEntry entry in listOfDevices)
				{
					if (entry.DeviceInformation.Id == deviceId)
					{
						return entry;
					}
				}
			}

			return null;
		}

		/// <summary>
		/// We must stop the DeviceWatchers because device watchers will continue to raise events even if
		/// the app is in suspension, which is not desired (drains battery). We resume the device watcher once the app resumes again.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="eventArgs"></param>
		private void OnAppSuspension(Object sender, SuspendingEventArgs args)
		{
			if (watchersStarted)
			{
				watchersSuspended = true;
				StopDeviceWatchers();
			}
			else
			{
				watchersSuspended = false;
			}
		}

		/// <summary>
		/// See OnAppSuspension for why we are starting the device watchers again
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		private void OnAppResume(Object sender, Object args)
		{
			if (watchersSuspended)
			{
				watchersSuspended = false;
				StartDeviceWatchers();
			}
		}

		/// <summary>
		/// We will remove the device from the UI
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="deviceInformationUpdate"></param>
		private async void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate deviceInformationUpdate)
		{
			await rootPage.Dispatcher.RunAsync(
				CoreDispatcherPriority.Normal,
				new DispatchedHandler(() =>
				{
					rootPage.NotifyUser("Device removed - " + deviceInformationUpdate.Id, NotifyType.StatusMessage);

					RemoveDeviceFromList(deviceInformationUpdate.Id);
				}));
		}

		/// <summary>
		/// This function will add the device to the listOfDevices so that it shows up in the UI
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="deviceInformation"></param>
		private async void OnDeviceAdded(DeviceWatcher sender, DeviceInformation deviceInformation)
		{
			await rootPage.Dispatcher.RunAsync(
				CoreDispatcherPriority.Normal,
				new DispatchedHandler(() =>
				{
					rootPage.NotifyUser("Device added - " + deviceInformation.Id, NotifyType.StatusMessage);

					AddDeviceToList(deviceInformation, mapDeviceWatchersToDeviceSelector[sender]);
				}));
		}

		/// <summary>
		/// Notify the UI whether or not we are connected to a device
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		private async void OnDeviceEnumerationComplete(DeviceWatcher sender, Object args)
		{
			await rootPage.Dispatcher.RunAsync(
				CoreDispatcherPriority.Normal,
				new DispatchedHandler(() =>
				{
					isAllDevicesEnumerated = true;

					// If we finished enumerating devices and the device has not been connected yet, the OnDeviceConnected method
					// is responsible for selecting the device in the device list (UI); otherwise, this method does that.
					if (EventHandlerForDevice.Current.IsDeviceConnected)
					{
						SelectDeviceInList(EventHandlerForDevice.Current.DeviceInformation.Id);

						ButtonDisconnectFromDevice.Content = ButtonNameDisconnectFromDevice;

						if (EventHandlerForDevice.Current.Device.PortName != "")
						{
							rootPage.NotifyUser("Connected to - " +
												EventHandlerForDevice.Current.Device.PortName +
												" - " +
												EventHandlerForDevice.Current.DeviceInformation.Id, NotifyType.StatusMessage);
						}
						else
						{
							rootPage.NotifyUser("Connected to - " +
												EventHandlerForDevice.Current.DeviceInformation.Id, NotifyType.StatusMessage);
						}
					}
					else if (EventHandlerForDevice.Current.IsEnabledAutoReconnect && EventHandlerForDevice.Current.DeviceInformation != null)
					{
						// We will be reconnecting to a device
						ButtonDisconnectFromDevice.Content = ButtonNameDisableReconnectToDevice;

						rootPage.NotifyUser("Waiting to reconnect to device -  " + EventHandlerForDevice.Current.DeviceInformation.Id, NotifyType.StatusMessage);
					}
					else
					{
						rootPage.NotifyUser("No device is currently connected", NotifyType.StatusMessage);
					}
				}));
		}

		private void OnDeviceConnected(EventHandlerForDevice sender, DeviceInformation deviceInformation)
		{
			// Find and select our connected device
			if (isAllDevicesEnumerated)
			{
				SelectDeviceInList(EventHandlerForDevice.Current.DeviceInformation.Id);

				ButtonDisconnectFromDevice.Content = ButtonNameDisconnectFromDevice;
			}

			if (EventHandlerForDevice.Current.Device.PortName != "")
			{
				rootPage.NotifyUser("Connected to - " +
									EventHandlerForDevice.Current.Device.PortName +
									" - " +
									EventHandlerForDevice.Current.DeviceInformation.Id, NotifyType.StatusMessage);
			}
			else
			{
				rootPage.NotifyUser("Connected to - " +
									EventHandlerForDevice.Current.DeviceInformation.Id, NotifyType.StatusMessage);
			}
		}

		/// <summary>
		/// The device was closed. If we will autoreconnect to the device, reflect that in the UI
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="deviceInformation"></param>
		private async void OnDeviceClosing(EventHandlerForDevice sender, DeviceInformation deviceInformation)
		{
			await rootPage.Dispatcher.RunAsync(
				CoreDispatcherPriority.Normal,
				new DispatchedHandler(() =>
				{
					// We were connected to the device that was unplugged, so change the "Disconnect from device" button
					// to "Do not reconnect to device"
					if (ButtonDisconnectFromDevice.IsEnabled && EventHandlerForDevice.Current.IsEnabledAutoReconnect)
					{
						ButtonDisconnectFromDevice.Content = ButtonNameDisableReconnectToDevice;
					}
				}));
		}

		private void SelectDeviceInList(String deviceIdToSelect)
		{
			// Don't select anything by default.
			COMcombobox.SelectedIndex = -1;

			for (int deviceListIndex = 0; deviceListIndex < listOfDevices.Count; deviceListIndex++)
			{
				if (listOfDevices[deviceListIndex].DeviceInformation.Id == deviceIdToSelect)
				{
					COMcombobox.SelectedIndex = deviceListIndex;

					break;
				}
			}
		}

		/// <summary>
		/// When ButtonConnectToDevice is disabled, ConnectDevices list will also be disabled.
		/// </summary>
		/// <param name="enableConnectButton">The state of ButtonConnectToDevice</param>
		private void UpdateConnectDisconnectButtonsAndList(Boolean enableConnectButton)
		{
			ButtonConnectToDevice.IsEnabled = enableConnectButton;
			ButtonDisconnectFromDevice.IsEnabled = !ButtonConnectToDevice.IsEnabled;

			COMcombobox.IsEnabled = ButtonConnectToDevice.IsEnabled;
		}

		private async void ButtonConnectToDevice_Click(object sender, RoutedEventArgs e)
		{
			var selection = COMcombobox.SelectedItem;
			DeviceListEntry entry = null;

			if (selection != null)
			{
				var obj = selection;
				entry = (DeviceListEntry)obj;

				if (entry != null)
				{
					// Create an EventHandlerForDevice to watch for the device we are connecting to
					EventHandlerForDevice.CreateNewEventHandlerForDevice();

					// Get notified when the device was successfully connected to or about to be closed
					EventHandlerForDevice.Current.OnDeviceConnected = this.OnDeviceConnected;
					EventHandlerForDevice.Current.OnDeviceClose = this.OnDeviceClosing;

					// It is important that the FromIdAsync call is made on the UI thread because the consent prompt, when present,
					// can only be displayed on the UI thread. Since this method is invoked by the UI, we are already in the UI thread.
					Boolean openSuccess = await EventHandlerForDevice.Current.OpenDeviceAsync(entry.DeviceInformation, entry.DeviceSelector);

					// Disable connect button if we connected to the device
					UpdateConnectDisconnectButtonsAndList(!openSuccess);
					if(openSuccess)
					{
						comText.IsReadOnly = false;

                        ResetReadCancellationTokenSource();
                        ResetWriteCancellationTokenSource();

                        IsReadTaskPending = true;
                        DataReaderObject = new DataReader(EventHandlerForDevice.Current.Device.InputStream);
                        await ReadAsync(ReadCancellationTokenSource.Token);
                    }
				}
			}
		}

		private void ButtonDisconnectFromDevice_Click(object sender, RoutedEventArgs e)
		{
			var selection = COMcombobox.SelectedItem;
			DeviceListEntry entry = null;

			// Prevent auto reconnect because we are voluntarily closing it
			// Re-enable the ConnectDevice list and ConnectToDevice button if the connected/opened device was removed.
			EventHandlerForDevice.Current.IsEnabledAutoReconnect = false;

			if (selection != null)
			{
				var obj = selection;
				entry = (DeviceListEntry)obj;

				if (entry != null)
				{
					EventHandlerForDevice.Current.CloseDevice();
					comText.IsReadOnly = true;
				}
			}
			UpdateConnectDisconnectButtonsAndList(true);
		}

		public void NotifyUser(string strMessage, NotifyType type)
		{
			// If called from the UI thread, then update immediately.
			// Otherwise, schedule a task on the UI thread to perform the update.
			if (Dispatcher.HasThreadAccess)
			{
				UpdateStatus(strMessage, type);
			}
			else
			{
				var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateStatus(strMessage, type));
			}
		}

		private void UpdateStatus(string strMessage, NotifyType type)
		{

			switch (type)
			{
				case NotifyType.StatusMessage:
					StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
					break;
				case NotifyType.ErrorMessage:
					StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
					break;
			}

			//comStatus.Text = strMessage;

			// Collapse the StatusBlock if it has no text to conserve real estate.

			StatusBorder.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
			if (StatusBlock.Text != String.Empty)
			{
				StatusBorder.Visibility = Visibility.Visible;
				StatusPanel.Visibility = Visibility.Visible;
			}
			else
			{
				StatusBorder.Visibility = Visibility.Collapsed;
				StatusPanel.Visibility = Visibility.Collapsed;
			}

			// Raise an event if necessary to enable a screen reader to announce the status update.
			var peer = FrameworkElementAutomationPeer.FromElement(StatusBlock);
			if (peer != null)
			{
				peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
			}
		}

		private void ComText_GotFocus(object sender, RoutedEventArgs e)
		{
			//comText.Background = new SolidColorBrush(Colors.DarkGray);
		}

		private void ComText_GettingFocus(UIElement sender, GettingFocusEventArgs args)
		{
			//comText.Background = new SolidColorBrush(Colors.DarkGray);
		}

        private async Task ReadAsync(CancellationToken cancellationToken)
        {

            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 1024;

            // Don't start any IO if we canceled the task
            lock (ReadCancelLock)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Cancellation Token will be used so we can stop the task operation explicitly
                // The completion function should still be called so that we can properly handle a canceled task
                DataReaderObject.InputStreamOptions = InputStreamOptions.Partial;
                loadAsyncTask = DataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);
            }

            UInt32 bytesRead = await loadAsyncTask;
            if (bytesRead > 0)
            {
                comText.Text += DataReaderObject.ReadString(bytesRead);
                comText.SelectionStart = comText.Text.Length;
                comText.SelectionLength = 0;
                ReadBytesCounter += bytesRead;
                //UpdateReadBytesCounterView();
            }
            //rootPage.NotifyUser("Read completed - " + bytesRead.ToString() + " bytes were read", NotifyType.StatusMessage);
        }

        private void ResetReadCancellationTokenSource()
        {
            // Create a new cancellation token source so that can cancel all the tokens again
            ReadCancellationTokenSource = new CancellationTokenSource();

            // Hook the cancellation callback (called whenever Task.cancel is called)
            ReadCancellationTokenSource.Token.Register(() => NotifyReadCancelingTask());
        }

        private void ResetWriteCancellationTokenSource()
        {
            // Create a new cancellation token source so that can cancel all the tokens again
            WriteCancellationTokenSource = new CancellationTokenSource();

            // Hook the cancellation callback (called whenever Task.cancel is called)
            WriteCancellationTokenSource.Token.Register(() => NotifyWriteCancelingTask());
        }

        private async void NotifyReadCancelingTask()
        {
            // Setting the dispatcher priority to high allows the UI to handle disabling of all the buttons
            // before any of the IO completion callbacks get a chance to modify the UI; that way this method
            // will never get the opportunity to overwrite UI changes made by IO callbacks
            await rootPage.Dispatcher.RunAsync(CoreDispatcherPriority.High,
                new DispatchedHandler(() =>
                {

                    if (!IsNavigatedAway)
                    {
                        rootPage.NotifyUser("Canceling Read... Please wait...", NotifyType.StatusMessage);
                    }
                }));
        }

        private async void NotifyWriteCancelingTask()
        {
            // Setting the dispatcher priority to high allows the UI to handle disabling of all the buttons
            // before any of the IO completion callbacks get a chance to modify the UI; that way this method
            // will never get the opportunity to overwrite UI changes made by IO callbacks
            await rootPage.Dispatcher.RunAsync(CoreDispatcherPriority.High,
                new DispatchedHandler(() =>
                {

                    if (!IsNavigatedAway)
                    {
                        rootPage.NotifyUser("Canceling Write... Please wait...", NotifyType.StatusMessage);
                    }
                }));
        }
    }
}
