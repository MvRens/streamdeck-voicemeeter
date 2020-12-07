using BarRaider.SdTools;
using HotkeyCommands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using VoiceMeeter.Midi;

namespace VoiceMeeter
{
    [PluginActionId("com.barraider.vmmultistate")]
    // Perhaps a base for this and VMAdvancedPressAction would be nice,
    // but as a proof of concept it is simply a modified copy
    class VMMultiStatePressAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    SetValue = String.Empty,
                    LongPressValue = String.Empty,
                    StateParam = String.Empty,
                    KeypressHotkey = String.Empty,
                    KeypressMidi = String.Empty,
                    LongHotkey = String.Empty,
                    LongMidi = String.Empty,
                    LongKeypressTime = LONG_KEYPRESS_LENGTH_MS.ToString()
                };

                return instance;
            }

            [JsonProperty(PropertyName = "setValue")]
            public string SetValue { get; set; }

            [JsonProperty(PropertyName = "longPressValue")]
            public string LongPressValue { get; set; }

            [JsonProperty(PropertyName = "stateParam")]
            public string StateParam { get; set; }

            [JsonProperty(PropertyName = "keypressHotkey")]
            public string KeypressHotkey { get; set; }

            [JsonProperty(PropertyName = "keypressMidi")]
            public string KeypressMidi { get; set; }

            [JsonProperty(PropertyName = "longHotkey")]
            public string LongHotkey { get; set; }

            [JsonProperty(PropertyName = "longMidi")]
            public string LongMidi { get; set; }

            [JsonProperty(PropertyName = "longKeypressTime")]
            public string LongKeypressTime { get; set; }
        }

        #region Private members

        private const int LONG_KEYPRESS_LENGTH_MS = 600;

        private readonly PluginSettings settings;
        private bool longKeyPressed = false;
        private int longKeypressTime = LONG_KEYPRESS_LENGTH_MS;
        private readonly System.Timers.Timer tmrRunLongPress = new System.Timers.Timer();

        #endregion

        #region Public Methods

        public VMMultiStatePressAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
                Connection.SetSettingsAsync(JObject.FromObject(settings));
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            tmrRunLongPress.Elapsed += TmrRunLongPress_Elapsed;

            InitializeSettings();
        }

        private void TmrRunLongPress_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            LongKeyPressed();
        }

        public void LongKeyPressed()
        {
            longKeyPressed = true;
            if (!String.IsNullOrEmpty(settings.LongPressValue))
            {
                VMManager.Instance.SetParameters(settings.LongPressValue);
            }
            MidiCommandHandler.HandleMidiParameters(settings.LongMidi);

            if (!String.IsNullOrEmpty(settings.LongHotkey))
            {
                HotkeyHandler.RunHotkey(settings.LongHotkey);
            }
        }

        #endregion

        #region PluginBase

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            InitializeSettings();
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Settings loaded: {payload.Settings}");
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            // Used for long press
            longKeyPressed = false;

            if (!VMManager.Instance.IsConnected)
            {
                await Connection.ShowAlert();
                return;
            }

            tmrRunLongPress.Interval = longKeypressTime > 0 ? longKeypressTime : LONG_KEYPRESS_LENGTH_MS;
            tmrRunLongPress.Start();
        }

        public override void KeyReleased(KeyPayload payload)
        {
            tmrRunLongPress.Stop();

            if (!longKeyPressed && !String.IsNullOrEmpty(settings.SetValue))
            {
                VMManager.Instance.SetParameters(settings.SetValue);
                MidiCommandHandler.HandleMidiParameters(settings.KeypressMidi);
                if (!String.IsNullOrEmpty(settings.KeypressHotkey))
                {
                    HotkeyHandler.RunHotkey(settings.KeypressHotkey);
                }
            }
        }

        public async override void OnTick()
        {
            if (!VMManager.Instance.IsConnected)
            {
                await Connection.SetImageAsync(Properties.Plugin.Default.VMNotRunning);
                return;
            }

            if (!String.IsNullOrEmpty(settings.StateParam))
            {
                string value = VMManager.Instance.GetParam(settings.StateParam);
                await Connection.SetStateAsync(value == Constants.ENABLED_VALUE ? (uint)0 : 1);
            }
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Destructor called");
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #endregion

        #region Private Methods

        private void InitializeSettings()
        {
            string keypressHotkey = HotkeyHandler.ParseKeystroke(settings.KeypressHotkey);
            string longHotkey = HotkeyHandler.ParseKeystroke(settings.LongHotkey);

            // If the parsed hotkey is different than what the user inputed, overwrite the user input
            // because it's invalid
            if (keypressHotkey != settings.KeypressHotkey || longHotkey != settings.LongHotkey)
            {
                settings.KeypressHotkey = keypressHotkey;
                settings.LongHotkey = longHotkey;
                SaveSettings();
            }

            if (!Int32.TryParse(settings.LongKeypressTime, out longKeypressTime))
            {
                settings.LongKeypressTime = LONG_KEYPRESS_LENGTH_MS.ToString();
                SaveSettings();
            }
        }

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        #endregion
    }
}
