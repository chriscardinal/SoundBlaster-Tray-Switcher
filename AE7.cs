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

            // Anything mentioning Sound Blaster.
            var sb = endpoints
                .Where(e => !string.IsNullOrEmpty(e.FriendlyName) &&
                            e.FriendlyName.IndexOf("Sound Blaster", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (sb.Count == 0)
                throw new InvalidOperationException(
                    "No active Sound Blaster render endpoint found.\r\n\r\n" +
                    "Available endpoints:\r\n  " +
                    string.Join("\r\n  ", endpoints.Select(e => e.FriendlyName)));

            // Rank by usefulness. The AE-9 in particular exposes BOTH a S/PDIF endpoint
            // ("Digital Audio (S/PDIF) (Sound Blaster AE-9s)") and an analog Speakers endpoint
            // ("Speakers (Sound Blaster AE-9)"). The relay-control parameter is meaningful
            // only on the analog endpoint; binding to S/PDIF gives E_FAIL on SetContext.
            //
            // Higher score = better candidate.
            Func<Native.AudioEndpoint, int> score = e =>
            {
                string n = e.FriendlyName ?? string.Empty;
                int s = 0;
                if (LooksDigital(n)) s -= 100;               // S/PDIF / optical / digital — last resort
                if (n.StartsWith("Speakers",   StringComparison.OrdinalIgnoreCase)) s += 20;
                if (n.StartsWith("Headphones", StringComparison.OrdinalIgnoreCase)) s += 15;
                if (n.StartsWith("Headphone",  StringComparison.OrdinalIgnoreCase)) s += 15;
                return s;
            };

            var match = sb.OrderByDescending(score).First();
            _endpointId   = match.Id;
            _endpointName = match.FriendlyName;

            var hw = new Native.StHardwareInfo
            {
                infoType = Native.HW_INFO_ENDPOINT_ID,
                info     = new Native.UtInfo { endpointId = match.Id },
            };
            try { _core.BindHardware(ref hw); }
            catch (Exception ex)
            { throw new InvalidOperationException("ISoundCore.BindHardware failed for endpoint '" + _endpointName + "': " + ex.Message, ex); }

            // SetContext can return E_FAIL on cards / modes that don't expose a per-context
            // parameter space (observed on AE-9 in some configurations). MultiplexOutput is a
            // device-level parameter, so this isn't actually required — swallow and keep going.
            // If the device truly doesn't expose MultiplexOutput, GetParamValue will surface
            // that error with a clear message.
            SetContextWarning = null;
            try { _core.SetContext(Native.CONTEXT_STANDARD, Native.RESTORE_LAST_STATE); }
            catch (Exception ex) { SetContextWarning = "SetContext(Standard) returned " + ex.Message; }
        }

        /// <summary>Non-null if SetContext failed (we treat it as non-fatal).</summary>
        public string SetContextWarning { get; private set; }

        private static bool LooksDigital(string n)
        {
            return n.IndexOf("Digital",  StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("S/PDIF",   StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("SPDIF",    StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Optical",  StringComparison.OrdinalIgnoreCase) >= 0;
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
            try { _core.GetParamValue(MakeMuxParam(), out val); }
            catch (Exception ex)
            { throw new InvalidOperationException("ISoundCore.GetParamValue(MultiplexOutput) failed: " + ex.Message, ex); }
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
            try { _core.SetParamValue(MakeMuxParam(), pv); }
            catch (Exception ex)
            { throw new InvalidOperationException("ISoundCore.SetParamValue(MultiplexOutput=" + value + ") failed: " + ex.Message, ex); }
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
