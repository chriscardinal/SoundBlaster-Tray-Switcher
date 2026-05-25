using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace SBQuickSwitch
{
    public enum OutputMode { Headphones, Speakers, Unknown }

    /// <summary>
    /// Wraps ISoundCore to flip the AE-7 multiplex output (the audible relay).
    /// </summary>
    internal sealed class AE7Controller : IDisposable
    {
        private object _comObj;
        private Native.ISoundCore _core;
        private string _endpointId;
        private string _endpointName;

        public string EndpointId   => _endpointId;
        public string EndpointName => _endpointName;

        public AE7Controller()
        {
            _comObj = new Native.CtHdaMgr();
            _core   = (Native.ISoundCore)_comObj;
        }

        /// <summary>
        /// Finds the AE-7 render endpoint and binds the ISoundCore session to it.
        /// Throws InvalidOperationException if no Sound Blaster endpoint is found.
        /// </summary>
        public void BindToAE7()
        {
            var endpoints = Native.EnumerateRenderEndpoints();
            // Prefer endpoints whose friendly name starts with "Sound Blaster".
            // The AE-7 typically surfaces as "Sound Blaster Speakers" or "Sound Blaster Speaker/Headphone".
            var match = endpoints.FirstOrDefault(e =>
                            !string.IsNullOrEmpty(e.FriendlyName) &&
                            e.FriendlyName.IndexOf("Sound Blaster", StringComparison.OrdinalIgnoreCase) >= 0);

            if (match == null)
                throw new InvalidOperationException(
                    "No active Sound Blaster render endpoint found.\r\n\r\n" +
                    "Available endpoints:\r\n  " +
                    string.Join("\r\n  ", endpoints.Select(e => e.FriendlyName)));

            _endpointId   = match.Id;
            _endpointName = match.FriendlyName;

            var hw = new Native.StHardwareInfo
            {
                infoType = Native.HW_INFO_ENDPOINT_ID,
                info     = new Native.UtInfo { endpointId = match.Id },
            };
            _core.BindHardware(ref hw);

            _core.SetContext(Native.CONTEXT_STANDARD, Native.RESTORE_LAST_STATE);
        }

        private static Native.StParam MakeMuxParam()
        {
            return new Native.StParam
            {
                paramId   = new Native.UtParamId { paramId = Native.PARAM_MULTIPLEX_OUTPUT },
                featureId = Native.FEATURE_MALCOLM_DEVICE_CONTROL,
                contextId = Native.CONTEXT_STANDARD,
            };
        }

        /// <summary>Read the current multiplex output value (raw enum int).</summary>
        public int GetMultiplexOutput()
        {
            Native.StParamValue val;
            _core.GetParamValue(MakeMuxParam(), out val);
            return (int)val.paramVal.dwordVal;
        }

        /// <summary>Set the multiplex output value.</summary>
        public void SetMultiplexOutput(int value)
        {
            var pv = new Native.StParamValue
            {
                paramType = Native.PARAM_TYPE_DWORD,
                paramVal  = new Native.UtParamValue { dwordVal = (uint)value },
            };
            _core.SetParamValue(MakeMuxParam(), pv);
        }

        public OutputMode GetMode()
        {
            int v = GetMultiplexOutput();
            if (v == Native.MUX_FRONTPANEL_HEADPHONE) return OutputMode.Headphones;
            if (v == Native.MUX_BACKPANEL_CENTERLFE)  return OutputMode.Speakers;
            return OutputMode.Unknown;
        }

        /// <summary>
        /// Flip between FrontPanel_Headphone and BackPanel_CenterLFE. Returns the new mode.
        /// </summary>
        public OutputMode Toggle()
        {
            int cur = GetMultiplexOutput();
            int next = (cur == Native.MUX_FRONTPANEL_HEADPHONE)
                     ? Native.MUX_BACKPANEL_CENTERLFE
                     : Native.MUX_FRONTPANEL_HEADPHONE;
            SetMultiplexOutput(next);
            return (next == Native.MUX_FRONTPANEL_HEADPHONE) ? OutputMode.Headphones : OutputMode.Speakers;
        }

        public void Dispose()
        {
            if (_comObj != null)
            {
                try { Marshal.ReleaseComObject(_comObj); } catch { }
                _comObj = null;
                _core = null;
            }
        }
    }
}
