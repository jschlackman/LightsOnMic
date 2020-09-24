using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Automation;
using Microsoft.Win32;
using LightFX;
using System.Windows.Interop;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using LightsOnMic.Properties;

namespace LightsOnMic
{
    /// <summary>
    /// Helper class from https://devblogs.microsoft.com/oldnewthing/20141013-00/?p=43863
    /// </summary>
    static class AutomationElementHelpers
    {
        public static AutomationElement
        Find(this AutomationElement root, string name)
        {
            return root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
        }

        public static IEnumerable<AutomationElement>
        EnumChildButtons(this AutomationElement parent)
        {
            return parent == null ? Enumerable.Empty<AutomationElement>()
                                  : parent.FindAll(TreeScope.Children,
              new PropertyCondition(AutomationElement.ControlTypeProperty,
                                    ControlType.Button)).Cast<AutomationElement>();
        }

        public static bool
        InvokeButton(this AutomationElement button)
        {
            var invokePattern = button.GetCurrentPattern(InvokePattern.Pattern)
                               as InvokePattern;
            if (invokePattern != null)
            {
                invokePattern.Invoke();
            }
            return invokePattern != null;
        }

        static public AutomationElement
        GetTopLevelElement(this AutomationElement element)
        {
            AutomationElement parent;
            while ((parent = TreeWalker.ControlViewWalker.GetParent(element)) !=
                 AutomationElement.RootElement)
            {
                element = parent;
            }
            return element;
        }

    }

    /// <summary>
    /// User color settings for defined statuses
    /// </summary>
    public class StatusColors
    {
        /// <summary>
        /// argb value of color to use when microphone is in use
        /// </summary>
        public int MicInUse;
        /// <summary>
        /// argb value of color to use when microphone is not in use
        /// </summary>
        public int MicNotInUse;
        /// <summary>
        /// argb value of color to use when session is locked
        /// </summary>
        public int SessionLocked;


        public StatusColors()
        {
            // Set default colors
            MicInUse = System.Drawing.Color.Red.ToArgb();
            MicNotInUse = System.Drawing.Color.FromArgb(0, 190, 0).ToArgb();
            SessionLocked = System.Drawing.Color.Yellow.ToArgb();
        }

    }

    /// <summary>
    /// AlienFX user settings
    /// </summary>
    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class ALFXSettings
    {
        /// <summary>
        /// Whether the user has enabled use of AlienFX lighting
        /// </summary>
        public bool Enabled;
        /// <summary>
        /// User color settings for AlienFX lights
        /// </summary>
        public StatusColors Colors;
        
        [NonSerialized]
        private LightFXController controller;

        /// <summary>
        /// Indicates whether AlienFX lighting is available on this system
        /// </summary>
        /// <returns></returns>
        public bool Available()
        {
            return (controller != null);
        }

        /// <summary>
        /// Indicates whether AlienFX lighting is available and enabled for use by the user
        /// </summary>
        /// <returns></returns>
        public bool Active()
        {
            return Enabled && Available();
        }


        public ALFXSettings()
        {
            Enabled = true;
            Colors = new StatusColors();
        }

        public string InitHardware()
        {
            string logMsg = "";
            // Release any existing instance
            if (controller != null) controller.LFX_Release();
            // Now create a new controller
            controller = new LightFXController();

            var result = controller.LFX_Initialize();
            if (result == LFX_Result.LFX_Success)
            {
                // Reset lights and update
                controller.LFX_Reset();
                controller.LFX_Update();

                logMsg = "AlienFX device initialized.";
            }
            else
            {
                switch (result)
                {
                    case LFX_Result.LFX_Error_NoDevs:
                        logMsg = "There is not AlienFX device available.";
                        break;
                    default:
                        logMsg = "There was an error initializing the AlienFX device.";
                        break;
                }
            }

            return logMsg;
        }

        public void ShutdownHardware()
        {
            if (Available())
            {
                controller.LFX_Reset();
                controller.LFX_Update();

                // Ensure any pending light commands have finished before releasing the light object
                System.Threading.Thread.Sleep(500);

                controller.LFX_Release();
            }
        }

        /// <summary>
        /// Set all AlienFX lights to a specified color
        /// </summary>
        /// <param name="color">Color to set</param>
        private void SetAllLights(LFX_ColorStruct color)
        {
            if (Available())
            {
                controller.LFX_GetNumDevices(out uint numDevs);

                for (uint devIndex = 0; devIndex < numDevs; devIndex++)
                {
                    controller.LFX_GetNumLights(devIndex, out uint numLights);

                    for (uint lightIndex = 0; lightIndex < numLights; lightIndex++)
                        controller.LFX_SetLightColor(devIndex, lightIndex, color);
                }

                controller.LFX_Update();
            }
        }

        /// <summary>
        /// Sets some AlienFX lights to a specified color
        /// </summary>
        /// <param name="color">Color to set</param>
        private void SetSomeLights(LFX_ColorStruct color)
        {
            if (Available())
            {
                controller.LFX_GetNumDevices(out uint numDevs);
                controller.LFX_Reset();

                for (uint devIndex = 0; devIndex < numDevs; devIndex++)
                {
                    controller.LFX_GetNumLights(devIndex, out uint numLights);

                    for (uint lightIndex = 0; lightIndex < numLights; lightIndex++)
                    {
                        // Check the light description and only set lights that are not the downlight
                        controller.LFX_GetLightDescription(devIndex, lightIndex, out StringBuilder description, 255);

                        if (!(description.ToString() == "downlight")) controller.LFX_SetLightColor(devIndex, lightIndex, color);
                    }
                }

                controller.LFX_Update();
            }
        }

        /// <summary>
        /// Set AlienFX lights to in-use status
        /// </summary>
        public void SetInUse()
        {
            SetAllLights(Colors.MicInUse.ToLFX());
        }

        /// <summary>
        /// Set AlienFX lights to not-in-use status
        /// </summary>
        public void SetNotInUse()
        {
            SetSomeLights(Colors.MicNotInUse.ToLFX());
        }

        /// <summary>
        /// Set AlienFX lights to locked status
        /// </summary>
        public void SetLocked()
        {
            SetSomeLights(Colors.SessionLocked.ToLFX());
        }

    }

    public static class ColorHelper
    {
        /// <summary>
        /// Convert an integer argb value to a brush
        /// </summary>
        /// <param name="argbColor">argb value to convert</param>
        /// <returns></returns>
        public static System.Windows.Media.Brush ToBrush(this int argbColor)
        {
            var color = System.Drawing.Color.FromArgb(argbColor);
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
        }

        /// <summary>
        /// Convert an argb value to an LFX_ColorStruct for use with AlienFX
        /// </summary>
        /// <param name="argbColor">argb value to convert</param>
        /// <returns></returns>
        public static LFX_ColorStruct ToLFX(this int argbColor)
        {
            var color = System.Drawing.Color.FromArgb(argbColor);
            return new LFX_ColorStruct(255, color.R, color.G, color.B);
        }

    }

    public static class ShellEvents
    {
        /// <summary>
        /// Automation object for Shell_TrayWnd
        /// </summary>
        private static AutomationElement shellTray;
        /// <summary>
        /// Automation object for the User Promoted Notification Area
        /// </summary>
        private static AutomationElement userArea;

        /// <summary>
        /// Enumerate the notification icons in a UI Automation object
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<AutomationElement> EnumNotificationIcons()
        {
            if (userArea != null)
            {
                foreach (var button in userArea.EnumChildButtons())
                {
                    yield return button;
                }
                foreach (var button in userArea.GetTopLevelElement().Find(
                              "System Promoted Notification Area").EnumChildButtons())
                {
                    yield return button;
                }
            }
        }

        /// <summary>
        /// Event handler object for subscribing to changes to the notification icons
        /// </summary>
        static StructureChangedEventHandler trayEventHandler;


        /// <summary>
        /// Dispose of references and hooks to the shell tray
        /// </summary>
        public static void DisposeTrayHooks()
        {
            if (trayEventHandler != null)
            {
                Automation.RemoveStructureChangedEventHandler(shellTray, trayEventHandler);
            }
        }

        /// <summary>
        /// Initialise references and hooks to elements of the tray window (yes, the tray, not the just the notification area which is PART of the shell tray).
        /// </summary>
        public static void InitTrayHooks(StructureChangedEventHandler eventHandler)
        {
            userArea = AutomationElement.RootElement.Find("User Promoted Notification Area");
            shellTray = userArea.GetTopLevelElement();

            Automation.AddStructureChangedEventHandler(shellTray, TreeScope.Descendants, trayEventHandler = eventHandler);
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        /// <summary>
        /// Indicate if the application should fully exit when closing a window.
        /// </summary>
        private bool ReallyExit = false;

        /// <summary>
        /// Notification icon for this application
        /// </summary>
        private static System.Windows.Forms.NotifyIcon notifyIcon;

        public MainWindow()
        {
            InitializeComponent();

            ShellEvents.InitTrayHooks(new StructureChangedEventHandler(OnStructureChanged));
            SystemEvents.SessionSwitch += new SessionSwitchEventHandler(OnSessionSwitch);

            InitNotifyIcon();

            if (Settings.Default.alfxSettings == null)
            {
                Settings.Default.alfxSettings = new ALFXSettings();
            }

            Settings.Default.alfxSettings.InitHardware();

            btnMicInUse.Background = Settings.Default.alfxSettings.Colors.MicInUse.ToBrush();
            btnMicNotInUse.Background = Settings.Default.alfxSettings.Colors.MicNotInUse.ToBrush();
            btnLocked.Background = Settings.Default.alfxSettings.Colors.SessionLocked.ToBrush();
        }


        public void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionUnlock:
                case SessionSwitchReason.ConsoleConnect:
                    CheckNotificationIcons();
                break;
                default:
                    Settings.Default.alfxSettings.SetLocked();
                break;
            }
        }

        /// <summary>
        /// Handles structure-changed events. If a new element has been added or removed, makes
        /// sure that notification icons are re-checked for mic use
        /// </summary>
        private void OnStructureChanged(object sender, StructureChangedEventArgs e)
        {
            // If an element was added or removed from the UI structure, check the notification icons
            if ((e.StructureChangeType == StructureChangeType.ChildAdded) || (e.StructureChangeType == StructureChangeType.ChildRemoved))
            {
                CheckNotificationIcons();
            }
        }

        /// <summary>
        /// Initialise the notificaion icon for this application
        /// </summary>
        private void InitNotifyIcon()
        {
            // Create an Exit menu item
            var ExitMenuItem = new ToolStripMenuItem()
            {
                Name = "ExitMenuItem",
                Text = "Exit"
            };
            ExitMenuItem.Click += ExitMenuItem_Click;

            // Create a Settings menu item
            var SettingsMenuItem = new ToolStripMenuItem()
            {
                Name = "SettingsMenuItem",
                Text = "Show Settings"
            };
            SettingsMenuItem.Click += SettingsMenuItem_Click;

            var SpacerMenuItem = new ToolStripSeparator();

            // Create the context menu for the notification icon
            var TrayIconContextMenu = new ContextMenuStrip()
            {
                Name = "TrayIconContextMenu"
            };

            // Add items to the menu
            TrayIconContextMenu.SuspendLayout();
            TrayIconContextMenu.Items.AddRange(new ToolStripItem[] {SettingsMenuItem, SpacerMenuItem, ExitMenuItem});
            TrayIconContextMenu.ResumeLayout(false);

            // Define the notification icon 
            notifyIcon = new NotifyIcon
            {
                Icon = LightsOnMic.Properties.Resources.NotifyIcon,
                Text = "LightsOnMic",
                ContextMenuStrip = TrayIconContextMenu,
                Visible = true,
            };

            notifyIcon.MouseDoubleClick += SettingsMenuItem_Click;

        }

        /// <summary>
        /// Check notification icons for a microphone in use icon and react accordingly
        /// </summary>
        /// <returns>Text names of all found notification icons</returns>
        private string CheckNotificationIcons()
        {
            string iconNames = "";
            
            // Query text labels of all notification icons
            foreach (var icon in ShellEvents.EnumNotificationIcons())
            {
                var name = icon.GetCurrentPropertyValue(AutomationElement.NameProperty) as string;

                // Append to list if not blank
                if (name != "")
                {
                    iconNames += name + '\n';
                }
            }

            if (iconNames.Contains(" is using your microphone\n"))
            {
                // Trigger mic in use lights
                Settings.Default.alfxSettings.SetInUse();

            }
            else
            {
                // Trigger mic not in use lights
                Settings.Default.alfxSettings.SetNotInUse();

            }

            return iconNames;

        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.Save();
            CheckNotificationIcons();
        }

        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            ReallyExit = true;
            this.Close();
        }

        private void SettingsMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Hide the window, don't actually quit unless we used an Exit button or menu
            if (!ReallyExit)
            {
                Hide();
                e.Cancel = true;
            }

            Settings.Default.Save();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Settings.Default.alfxSettings.ShutdownHardware();
            ShellEvents.DisposeTrayHooks();
            notifyIcon.Dispose();
        }

        private void BtnMicInUse_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog()
            {
                Color = System.Drawing.Color.FromArgb(Settings.Default.alfxSettings.Colors.MicInUse),
                
            };

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Settings.Default.alfxSettings.Colors.MicInUse = colorDialog.Color.ToArgb();
                btnMicInUse.Background = Settings.Default.alfxSettings.Colors.MicInUse.ToBrush();
            }
        }

        private void BtnMicNotInUse_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog()
            {
                Color = System.Drawing.Color.FromArgb(Settings.Default.alfxSettings.Colors.MicNotInUse)
            };

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Settings.Default.alfxSettings.Colors.MicNotInUse = colorDialog.Color.ToArgb();
                btnMicNotInUse.Background = Settings.Default.alfxSettings.Colors.MicNotInUse.ToBrush();
            }
        }

        private void BtnLocked_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog()
            {
                Color = System.Drawing.Color.FromArgb(Settings.Default.alfxSettings.Colors.SessionLocked),

            };

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Settings.Default.alfxSettings.Colors.SessionLocked = colorDialog.Color.ToArgb();
                btnLocked.Background = Settings.Default.alfxSettings.Colors.SessionLocked.ToBrush();
            }
        }

        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            txtDebugLog.Text += "Found tray icons:\n" + CheckNotificationIcons();
        }
    }
}
