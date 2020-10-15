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
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using LightsOnMic.Properties;
using System.Timers;
using System.Collections.ObjectModel;

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

    // Text resource code adapted from https://www.martinstoeckli.ch/csharp/csharp.html#windows_text_resources

    /// <summary>
    /// Searches for a text resource in a Windows library, allowing use of existing Windows resources for language independence.
    /// </summary>
    internal class WindowsStrings
    {

        /// <summary>
        /// Searches for a text resource in a Windows library.
        /// </summary>
        /// <example>
        ///   btnCancel.Text = WindowsStrings.Load("user32.dll", 801, "Cancel");
        ///   btnYes.Text = WindowsStrings.Load("user32.dll", 805, "Yes");
        /// </example>
        /// <param name="libraryName">Name of the windows library, e.g. "user32.dll" or "shell32.dll"</param>
        /// <param name="ident">ID of the string resource.</param>
        /// <param name="defaultText">Return this text, if the resource string could not be found.</param>
        /// <returns>Requested string if the resource was found, otherwise the <paramref name="defaultText"/></returns>
        private static string Load(IntPtr libraryHandle, uint ident, string defaultText)
        {
            if (libraryHandle != IntPtr.Zero)
            {
                StringBuilder sb = new StringBuilder(1024);
                int size = LoadString(libraryHandle, ident, sb, 1024);
                if (size > 0)
                    return sb.ToString();
            }
            return defaultText;
        }

        /// <summary>
        /// Gets a list of known strings that may be used to indicate the microphone is in use.
        /// </summary>
        /// <returns></returns>
        public static List<string> GetMicUseStrings()
        {
            List<string> moduleStrings = new List<string>();

            // Get a new handle to the external library containing strings used for the mic in use notification
            IntPtr libraryHandle = LoadLibrary("sndvolsso.dll");

            if (libraryHandle != IntPtr.Zero)
            {
                // Load each known string resource (using en-US defaults if not found)
                moduleStrings.Add(Load(libraryHandle, 2045, "Your microphone is currently in use"));
                moduleStrings.Add(Load(libraryHandle, 2046, "%1 is using your microphone").Remove(0, 3));
                moduleStrings.Add(Load(libraryHandle, 2047, "%1 apps are using your microphone").Remove(0, 3));
                moduleStrings.Add(Load(libraryHandle, 2052, "1 app is using your microphone"));

                // Free handle to external library
                FreeLibrary("sndvolsso.dll");
            }

            return moduleStrings;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FreeLibrary(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int LoadString(IntPtr hInstance, uint uID, StringBuilder lpBuffer, int nBufferMax);
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

        /// <summary>
        /// Sets the mic in use status to blink when active
        /// </summary>
        public bool BlinkMicInUse;
        //public bool BlinkMicNotInUse;
        //public bool BlinkSessionLocked;

        public StatusColors()
        {
            // Set default colors
            MicInUse = System.Drawing.Color.Red.ToArgb();
            MicNotInUse = System.Drawing.Color.FromArgb(0, 190, 0).ToArgb();
            SessionLocked = System.Drawing.Color.Yellow.ToArgb();

            BlinkMicInUse = false;
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

        /// <summary>
        /// Current color to blink with
        /// </summary>
        [NonSerialized]
        private int currentColor;

        /// <summary>
        /// Timer object for light blink
        /// </summary>
        [NonSerialized]
        private System.Timers.Timer blinkTimer;

        /// <summary>
        /// Indicates whether a blinking light is currently in the high or low brightness state
        /// </summary>
        [NonSerialized]
        private bool blinkHigh;

        // Low brightness value for blinking
        [NonSerialized] 
        private const byte blinkLowBrightness = 32;

        /// <summary>
        /// Number of ms for low blink state
        /// </summary>
        private const int blinkPeriod = 1250;


        public ALFXSettings()
        {
            Enabled = true;
            Colors = new StatusColors();
        }

        public string InitHardware()
        {
            string logMsg;
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
                        logMsg = "There is no AlienFX device available.";
                        break;
                    default:
                        logMsg = "There was an error initializing the AlienFX device.";
                        break;
                }
            }

            return logMsg;
        }

        /// <summary>
        /// Shutdown AlienFX lighting
        /// </summary>
        public void ShutdownHardware()
        {
            StopBlink();

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
        /// Gets the descriptions of all currently connected AlienFX lights grouped by device
        /// </summary>
        /// <returns></returns>
        public List<RGBLightGroup> GetConnectedLights()
        {
            List<RGBLightGroup> ltDevices = new List<RGBLightGroup>();

            if (Available())
            {
                LFX_Result result;
                controller.LFX_GetNumDevices(out uint numDevices);

                for (uint devIndex = 0; devIndex < numDevices; devIndex++)
                {
                    StringBuilder description;
                    result = controller.LFX_GetDeviceDescription(devIndex, out description, 255, out LFX_DeviceType devType);
                    if (result == LFX_Result.LFX_Success)
                    {
                        RGBLightGroup ltDevice = new RGBLightGroup() { Description = description.ToString(), Members = new List<RGBLight>() };

                        // Enumerate lights connected to this device
                        result = controller.LFX_GetNumLights(devIndex, out uint numLights);
                        if (result == LFX_Result.LFX_Success)
                        {
                            for (uint lightIndex = 0; lightIndex < numLights; lightIndex++)
                            {
                                result = controller.LFX_GetLightDescription(devIndex, lightIndex, out description, 255);
                                if (result == LFX_Result.LFX_Success)
                                {
                                    RGBLight ltLight = new RGBLight() { Description = description.ToString() };
                                    ltLight.SetValue(ItemHelper.ParentProperty, ltDevice);
                                    ltDevice.Members.Add(ltLight);
                                }
                            }
                        }

                        // Add this device if we got the name of at least 1 light
                        if (ltDevice.Members.Count > 0)
                        {
                            ltDevices.Add(ltDevice);
                        }
                    }
                }
            }

            return ltDevices;
        }

        /// <summary>
        /// Set all AlienFX lights to a specified color
        /// </summary>
        /// <param name="color">Color to set</param>
        private void SetAllLights(int color, byte brightness = 255)
        {
            if (Available())
            {
                controller.LFX_GetNumDevices(out uint numDevs);

                for (uint devIndex = 0; devIndex < numDevs; devIndex++)
                {
                    controller.LFX_GetNumLights(devIndex, out uint numLights);

                    for (uint lightIndex = 0; lightIndex < numLights; lightIndex++)
                        controller.LFX_SetLightColor(devIndex, lightIndex, color.ToLFX(brightness));
                }

                controller.LFX_Update();
            }
        }

        /// <summary>
        /// Blink lights with the current color
        /// </summary>
        private void BlinkAllLights()
        {
            if (Available())
            {
                if (blinkTimer == null)
                {
                    blinkTimer = new System.Timers.Timer(blinkPeriod);
                    blinkTimer.Elapsed += BlinkTimer_Elapsed;
                }
                
                blinkHigh = true;
                blinkTimer.Start();
            }
        }

        private void BlinkTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            blinkHigh = !blinkHigh;
            if (blinkHigh)
            {
                //blinkTimer.Interval = blinkPeriod * 2;
                SetAllLights(currentColor);
            }
            else
            {
                //blinkTimer.Interval = blinkPeriod;
                SetAllLights(currentColor, blinkLowBrightness);
            }
            
        }

        /// <summary>
        /// Sets some AlienFX lights to a specified color
        /// </summary>
        /// <param name="color">Color to set</param>
        private void SetSomeLights(int color)
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

                        if (!(description.ToString() == "downlight")) controller.LFX_SetLightColor(devIndex, lightIndex, color.ToLFX());
                    }
                }

                controller.LFX_Update();
            }
        }

        /// <summary>
        /// Stop blinking the lights
        /// </summary>
        private void StopBlink()
        {
            if (blinkTimer != null) blinkTimer.Stop();
        }

        /// <summary>
        /// Set AlienFX lights to in-use status
        /// </summary>
        public void SetInUse()
        {
            StopBlink();
            currentColor = Colors.MicInUse;
            SetAllLights(Colors.MicInUse);
            if (Colors.BlinkMicInUse) BlinkAllLights();
        }

        /// <summary>
        /// Set AlienFX lights to not-in-use status
        /// </summary>
        public void SetNotInUse()
        {
            StopBlink();
            currentColor = Colors.MicNotInUse;
            SetSomeLights(Colors.MicNotInUse);
        }

        /// <summary>
        /// Set AlienFX lights to locked status
        /// </summary>
        public void SetLocked()
        {
            StopBlink();
            currentColor = Colors.SessionLocked;
            SetSomeLights(Colors.SessionLocked);
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
        /// <param name="brightness">brightness value (0-255)</param>
        /// <returns></returns>
        public static LFX_ColorStruct ToLFX(this int argbColor, byte brightness = 255)
        {
            var color = System.Drawing.Color.FromArgb(argbColor);
            return new LFX_ColorStruct(brightness, color.R, color.G, color.B);
        }

    }


    public class RGBLightGroup : DependencyObject
    {
        public string Description { get; set; }
        public List<RGBLight> Members { get; set; }
    }

    public class RGBLight : DependencyObject
    {
        public string Description { get; set; }
        public bool SetLight { get; set; }
    }

    public class ItemHelper : DependencyObject
    {
        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.RegisterAttached("IsChecked", typeof(bool?), typeof(ItemHelper), new PropertyMetadata(false, new PropertyChangedCallback(OnIsCheckedPropertyChanged)));
        private static void OnIsCheckedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RGBLightGroup && ((bool?)e.NewValue).HasValue)
                foreach (RGBLight p in (d as RGBLightGroup).Members)
                    ItemHelper.SetIsChecked(p, (bool?)e.NewValue);

            if (d is RGBLight)
            {
                RGBLight lt = d as RGBLight;
                lt.SetLight = (bool)e.NewValue;

                int rgbChecked = (lt.GetValue(ItemHelper.ParentProperty) as RGBLightGroup).Members.Where(x => ItemHelper.GetIsChecked(x) == true).Count();
                int rgbUnchecked = (lt.GetValue(ItemHelper.ParentProperty) as RGBLightGroup).Members.Where(x => ItemHelper.GetIsChecked(x) == false).Count();
                if (rgbChecked > 0 && rgbUnchecked > 0)
                {
                    ItemHelper.SetIsChecked(lt.GetValue(ItemHelper.ParentProperty) as DependencyObject, null);
                    return;
                }
                if (rgbChecked > 0)
                {
                    ItemHelper.SetIsChecked(lt.GetValue(ItemHelper.ParentProperty) as DependencyObject, true);
                    return;
                }
                ItemHelper.SetIsChecked(lt.GetValue(ItemHelper.ParentProperty) as DependencyObject, false);
                }
                }
                public static void SetIsChecked(DependencyObject element, bool? IsChecked)
                {
                    element.SetValue(ItemHelper.IsCheckedProperty, IsChecked);
                }
                public static bool? GetIsChecked(DependencyObject element)
                {
                    return (bool?)element.GetValue(ItemHelper.IsCheckedProperty);
                }

        public static readonly DependencyProperty ParentProperty = DependencyProperty.RegisterAttached("Parent", typeof(object), typeof(ItemHelper));
        public static void SetParent(DependencyObject element, object Parent)
        {
            element.SetValue(ItemHelper.ParentProperty, Parent);
        }
        public static object GetParent(DependencyObject element)
        {
            return (object)element.GetValue(ItemHelper.ParentProperty);
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
        /// String values used by notification icons that indicate the microphone is in use.
        /// </summary>
        private readonly List<string> InUseText;

        /// <summary>
        /// Notification icon for this application
        /// </summary>
        private static System.Windows.Forms.NotifyIcon notifyIcon;

        private static SessionSwitchEventHandler SessionSwitchHandler;

        public ObservableCollection<RGBLightGroup> Lights { get; set; }

        private const string regWindowsRunKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string regProgramValue = "LightsOnMic";

        public MainWindow()
        {
            InitializeComponent();

            ShellEvents.InitTrayHooks(new StructureChangedEventHandler(OnStructureChanged));
            SystemEvents.SessionSwitch += SessionSwitchHandler = new SessionSwitchEventHandler(OnSessionSwitch);

            InitNotifyIcon();

            if (Settings.Default.alfxSettings == null)
            {
                Settings.Default.alfxSettings = new ALFXSettings();
            }

            InUseText = WindowsStrings.GetMicUseStrings();
            
            txtDebugLog.Text += Settings.Default.alfxSettings.InitHardware();

            btnMicInUse.Background = Settings.Default.alfxSettings.Colors.MicInUse.ToBrush();
            btnMicNotInUse.Background = Settings.Default.alfxSettings.Colors.MicNotInUse.ToBrush();
            btnLocked.Background = Settings.Default.alfxSettings.Colors.SessionLocked.ToBrush();

            chkInUseBlink.IsChecked = Settings.Default.alfxSettings.Colors.BlinkMicInUse;

            // Check if program is correctly set to run at logon
            chkStartAtLogon.IsChecked = (Registry.CurrentUser.OpenSubKey(regWindowsRunKey).GetValue(regProgramValue,"").ToString() == "\"" + System.Windows.Forms.Application.ExecutablePath + "\"");

            // Load current lights into treeview
            Lights = new ObservableCollection<RGBLightGroup>(Settings.Default.alfxSettings.GetConnectedLights());

            CheckNotificationIcons();

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
        /// Initialise the notification icon for this application
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

            // Check if any of the in use strings are currently being displayed by a notification icon
            if (InUseText.Any(s => iconNames.Contains(s + "\n")))
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

        private void BtnDone_Click(object sender, RoutedEventArgs e)
        {
            BtnApply_Click(sender, e);
            Hide();
        }

        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            ReallyExit = true;
            this.Close();
        }

        private void SettingsMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
            WindowState = storedWindowState;
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
            SystemEvents.SessionSwitch -= SessionSwitchHandler;
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

        /// <summary>
        /// Stores the windows state prior to being minimized to the notification area
        /// </summary>
        private WindowState storedWindowState = WindowState.Normal;

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
            else
            {
                storedWindowState = WindowState;
            }

        }

        private void ChkInUseBlink_Changed(object sender, RoutedEventArgs e)
        {
            Settings.Default.alfxSettings.Colors.BlinkMicInUse = (bool)chkInUseBlink.IsChecked;
        }

        private void ChkStartAtLogon_Changed(object sender, RoutedEventArgs e)
        {
            // Get reference to the current user's Windows Run key with edit permissions
            RegistryKey windowsRun = Registry.CurrentUser.OpenSubKey(regWindowsRunKey, true);

            // If checked, set the correct registry value
            if ((bool)chkStartAtLogon.IsChecked)
            {
                windowsRun.SetValue(regProgramValue, "\"" + System.Windows.Forms.Application.ExecutablePath + "\"");
            }
            else
            {
                // Check for the presence of the startup registry value and remove it
                if (windowsRun.GetValue(regProgramValue) != null)
                {
                    windowsRun.DeleteValue(regProgramValue);
                }
            }
        }

    }
}
