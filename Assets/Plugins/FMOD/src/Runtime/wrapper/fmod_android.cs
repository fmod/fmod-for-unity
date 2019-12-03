#if UNITY_ANDROID
using System.Runtime.InteropServices;

namespace FMOD
{
    static class Android
    {
        public static RESULT setThreadAffinity(ref THREADAFFINITY affinity)
        {
            return FMOD_Android_SetThreadAffinity(ref affinity);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct THREADAFFINITY
        {
            public THREAD mixer;              /* Software mixer thread. */
            public THREAD stream;             /* Stream thread. */
            public THREAD nonblocking;        /* Asynchronous sound loading thread. */
            public THREAD file;               /* File thread. */
            public THREAD geometry;           /* Geometry processing thread. */
            public THREAD profiler;           /* Profiler thread. */
            public THREAD studioUpdate;       /* Studio update thread. */
            public THREAD studioLoadBank;     /* Studio bank loading thread. */
            public THREAD studioLoadSample;   /* Studio sample loading thread. */
        }

        [global::System.Flags]
        public enum THREAD : uint
        {
            DEFAULT = 0,        /* Use default thread assignment. */
            CORE0 = 1 << 0,
            CORE1 = 1 << 1,
            CORE2 = 1 << 2,
            CORE3 = 1 << 3,
            CORE4 = 1 << 4,
            CORE5 = 1 << 5,
            CORE6 = 1 << 6,
            CORE7 = 1 << 7,
        }

        [DllImport(VERSION.dll)]
        private static extern RESULT FMOD_Android_SetThreadAffinity(ref THREADAFFINITY affinity);
    }
}

#endif