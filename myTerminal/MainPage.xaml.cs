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
using Windows.ApplicationModel.Core;
using Windows.UI.ViewManagement;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml.Automation.Peers;

// 빈 페이지 항목 템플릿에 대한 설명은 https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x412에 나와 있습니다.

namespace myTerminal
{
    /// <summary>
    /// 자체적으로 사용하거나 프레임 내에서 탐색할 수 있는 빈 페이지입니다.
    /// </summary>
    public sealed partial class MainPage : Page
    {
		public static MainPage Current;

		public MainPage()
        {
            this.InitializeComponent();
			Current = this;
			CustomizeTitleBar();
		}

		private void CustomizeTitleBar()
		{
			// customize title area
			CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
			Window.Current.SetTitleBar(customTitleBar);

			// customize buttons' colors
			ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
			titleBar.BackgroundColor = Colors.Black;
			titleBar.ButtonBackgroundColor = Colors.Black;
			titleBar.ForegroundColor = Colors.White;
			titleBar.ButtonForegroundColor = Colors.White;
			titleBar.ButtonInactiveBackgroundColor = Colors.Black;
			titleBar.ButtonInactiveForegroundColor = Colors.White;
		}

		private void NavigationTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
		{

		}

		private void NavigationButton_Click(object sender, RoutedEventArgs e)
		{
			mainSplitView.IsPaneOpen = !mainSplitView.IsPaneOpen;
		}

		private void PaneTCPButton_Click(object sender, RoutedEventArgs e)
		{
			Navigate(typeof(TCPPage));
		}

		private void PaneCOMButton_Click(object sender, RoutedEventArgs e)
		{
			Navigate(typeof(COMPage));
		}

		public bool Navigate(Type sourcePageType, object parameter = null)
		{
			return SplitViewFrame.Navigate(sourcePageType, parameter);
		}

		private void SettingButton_Click(object sender, RoutedEventArgs e)
		{
			Navigate(typeof(settingPage));
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
	}
	public enum NotifyType
	{
		StatusMessage,
		ErrorMessage
	};
}
