using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SBQuickSwitch
{
    internal static class Native
    {
        // ===== Sound Blaster Command "Malcolm" parameter constants =====

        public const uint FEATURE_MALCOLM_DEVICE_CONTROL = 0x01000001;
        public const int PARAM_MULTIPLEX_OUTPUT          = 15;

        public const int MUX_FRONTPANEL_HEADPHONE = 0; // ACM headphone jack
        public const int MUX_BACKPANEL_CENTERLFE  = 1; // back-panel multichannel speakers
        public const int MUX_BACKPANEL_HEADPHONE  = 2;
        public const int MUX_MIC_IN               = 3;

        public const int CONTEXT_STANDARD     = 1;
        public const int RESTORE_LAST_STATE   = 0;

        public const int HW_INFO_ENDPOINT_ID = 0;
        public const int HW_INFO_ADAPTER_ID  = 1;

        // _etParamType
        public const int PARAM_TYPE_BOOL    = 0;
        public const int PARAM_TYPE_DWORD   = 1;
        public const int PARAM_TYPE_FLOAT   = 2;
        public const int PARAM_TYPE_LONGINT = 3;
        public const int PARAM_TYPE_SELECT  = 4;
        public const int PARAM_TYPE_VARSIZE = 5;

        // ===== ISoundCore COM marshalling =====

        // _utInfo  (520 bytes — UInt16[260])
        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
        public struct UtInfo
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string endpointId;
        }

        // _stHardwareInfo
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct StHardwareInfo
        {
            public int infoType;   // _etHardwareInfoType
            public UtInfo info;
        }

        // _utParamId — explicit-layout union, 4 bytes total
        [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 4)]
        public struct UtParamId
        {
            [FieldOffset(0)] public int paramId;
        }

        // _stParam — 12 bytes (paramId 4 + featureId 4 + contextId 4)
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct StParam
        {
            public UtParamId paramId;
            public uint featureId; // _etFeature
            public uint contextId; // _etContext
        }

        // _utParamValue — explicit-layout union, 4 bytes
        [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 4)]
        public struct UtParamValue
        {
            [FieldOffset(0)] public float floatVal;
            [FieldOffset(0)] public int boolVal;
            [FieldOffset(0)] public uint dwordVal;
            [FieldOffset(0)] public int longVal;
        }

        // _stParamValue — 8 bytes
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct StParamValue
        {
            public int paramType; // _etParamType
            public UtParamValue paramVal;
        }

        // _stContextInfo  — 4 (etContext) + 32 (sbyte[32]) = 36 bytes
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct StContextInfo
        {
            public uint contextId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] szDescription;
        }

        // _stFeatureInfo  — 4 + 32 + 16 = 52 bytes
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct StFeatureInfo
        {
            public uint featureId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] szDescription;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] szVersion;
        }

        // _stParamInfo  — 12 + 4 + 4 + 8*4 + 4 + 32 = 88 bytes
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct StParamInfo
        {
            public StParam paramId;
            public int paramType;
            public uint paramDataSize;
            public StParamValue minVal;
            public StParamValue maxVal;
            public StParamValue stepSize;
            public StParamValue defaultVal;
            public uint paramAttrib;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] szDescription;
        }

        // _PARAM_DATA — 8 bytes (size + IntPtr)
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct ParamData
        {
            public uint paramSize;
            public IntPtr paramData;
        }

        // _stParamData — 4-byte ulong + 4-byte enum = approximated as 8 bytes filler.
        // We never call these methods, so the exact contents don't matter as long as
        // the slot's size is correct in the vtable. Use a stand-in.
        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
        public struct StParamDataFiller { }

        // ISoundCore — all 18 methods MUST be declared in vtable order.
        [ComImport]
        [Guid("6111E7C4-3EA4-47ED-B074-C638875282C4")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ISoundCore
        {
            void BindHardware(ref StHardwareInfo hardwareInfo);

            void EnumContexts(uint index, out StContextInfo contextInfo);

            void GetContextInfo(uint contextId, out StContextInfo contextInfo);

            void GetContext(out uint contextId);

            void SetContext(uint contextId, uint restoreState);

            void EnumFeatures(uint contextId, uint index, out StFeatureInfo featureInfo);

            void GetFeatureInfo(uint contextId, uint featureId, out StFeatureInfo featureInfo);

            void EnumParams(uint contextId, uint index, uint featureId, out StParamInfo paramInfo);

            void GetParamInfo(StParam param, out StParamInfo paramInfo);

            void GetParamValue(StParam param, out StParamValue paramValue);

            void SetParamValue(StParam param, StParamValue paramValue);

            void GetParamValueEx(StParam param, ref uint paramSize, ref StParamDataFiller paramData);

            void SetParamValueEx(StParam param, uint paramSize, ref StParamDataFiller paramData);

            void ValidateParamValue(StParam param, StParamValue paramValue);

            void ValidateParamValueEx(StParam param, uint paramSize, ref StParamDataFiller paramData);

            void GetParamValueEx_unsafe(StParam param, ref ParamData paramData);

            void SetParamValueEx_unsafe(StParam param, ref ParamData paramData);

            void ValidateParamValueEx_unsafe(StParam param, ref ParamData paramData);
        }

        // CtHdaMgr CoClass — registered (32-bit view) at HKLM\SOFTWARE\Classes\WOW6432Node\CLSID\{3C0E7BA7-...}
        // ProgID: CtHdaCtl.CtHdaMgr
        [ComImport]
        [Guid("3C0E7BA7-F9C3-460F-BCBE-FC91A06EF3F3")]
        public class CtHdaMgr { }

        // ===== Core Audio (MMDevice API) — minimal declarations =====

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        public class MMDeviceEnumerator { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out IMMDeviceCollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
            [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int Item(uint index, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object obj);
            [PreserveSig] int OpenPropertyStore(uint stgmAccess, out IPropertyStore propertyStore);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetState(out uint state);
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IPropertyStore
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int GetAt(uint index, out PROPERTYKEY key);
            [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
            [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
            [PreserveSig] int Commit();
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;
            public PROPERTYKEY(Guid g, uint p) { fmtid = g; pid = p; }
        }

        // PROPVARIANT is large (~24 bytes on x86). We only read string values.
        [StructLayout(LayoutKind.Sequential)]
        public struct PROPVARIANT
        {
            public ushort vt;
            public ushort wReserved1;
            public ushort wReserved2;
            public ushort wReserved3;
            public IntPtr p; // pwszVal lives here for VT_LPWSTR(31)
            public IntPtr pad1;
            public IntPtr pad2;

            public string AsString()
            {
                if (vt == 31 /* VT_LPWSTR */ && p != IntPtr.Zero)
                    return Marshal.PtrToStringUni(p);
                return null;
            }
        }

        [DllImport("ole32.dll")]
        public static extern int PropVariantClear(ref PROPVARIANT pvar);

        // Property keys from Windows mmdeviceapi.h and Creative's CtxHda.inf
        public static readonly PROPERTYKEY PKEY_Device_FriendlyName =
            new PROPERTYKEY(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
        public static readonly PROPERTYKEY PKEY_AudioEndpoint_GUID =
            new PROPERTYKEY(new Guid("1da5d803-d492-4edd-8c23-e0c0ffee7f0e"), 4);

        public const int eRender                  = 0;
        public const uint DEVICE_STATE_ACTIVE     = 0x00000001;
        public const uint STGM_READ               = 0;

        // ===== Endpoint discovery =====

        public sealed class AudioEndpoint
        {
            public string Id;
            public string FriendlyName;
        }

        public static List<AudioEndpoint> EnumerateRenderEndpoints()
        {
            var result = new List<AudioEndpoint>();
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            try
            {
                IMMDeviceCollection col;
                int hr = enumerator.EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE, out col);
                Marshal.ThrowExceptionForHR(hr);
                try
                {
                    uint count;
                    Marshal.ThrowExceptionForHR(col.GetCount(out count));
                    for (uint i = 0; i < count; i++)
                    {
                        IMMDevice dev;
                        Marshal.ThrowExceptionForHR(col.Item(i, out dev));
                        try
                        {
                            string id;
                            Marshal.ThrowExceptionForHR(dev.GetId(out id));
                            IPropertyStore ps;
                            Marshal.ThrowExceptionForHR(dev.OpenPropertyStore(STGM_READ, out ps));
                            try
                            {
                                PROPERTYKEY k = PKEY_Device_FriendlyName;
                                PROPVARIANT pv;
                                Marshal.ThrowExceptionForHR(ps.GetValue(ref k, out pv));
                                string name = pv.AsString();
                                PropVariantClear(ref pv);
                                result.Add(new AudioEndpoint { Id = id, FriendlyName = name ?? string.Empty });
                            }
                            finally { Marshal.ReleaseComObject(ps); }
                        }
                        finally { Marshal.ReleaseComObject(dev); }
                    }
                }
                finally { Marshal.ReleaseComObject(col); }
            }
            finally { Marshal.ReleaseComObject(enumerator); }
            return result;
        }
    }
}
