using System;
using System.Runtime.InteropServices;

namespace PDFiumSharp.Types
{ // ReSharper disable once InconsistentNaming
    [StructLayout(LayoutKind.Sequential)]
    public class FPDF_FORMFILLINFO
    {
        public FPDF_FORMFILLINFO(int version) => this.version = version;

        private readonly int version;

        public int Version => this.version;

        private readonly IntPtr Release;

        private readonly IntPtr FFI_Invalidate;

        private readonly IntPtr FFI_OutputSelectedRect;

        private readonly IntPtr FFI_SetCursor;

        private readonly IntPtr FFI_SetTimer;

        private readonly IntPtr FFI_KillTimer;

        private readonly IntPtr FFI_GetLocalTime;

        private readonly IntPtr FFI_OnChange;

        private readonly IntPtr FFI_GetPage;

        private readonly IntPtr FFI_GetCurrentPage;

        private readonly IntPtr FFI_GetRotation;

        private readonly IntPtr FFI_ExecuteNamedAction;

        private readonly IntPtr FFI_SetTextFieldFocus;

        private readonly IntPtr FFI_DoURIAction;

        private readonly IntPtr FFI_DoGoToAction;

        private readonly IntPtr m_pJsPlatform;

        private readonly IntPtr FFI_DisplayCaret;

        private readonly IntPtr FFI_GetCurrentPageIndex;

        private readonly IntPtr FFI_SetCurrentPage;

        private readonly IntPtr FFI_GotoURL;

        private readonly IntPtr FFI_GetPageViewRect;

        private readonly IntPtr FFI_PageEvent;

        private readonly IntPtr FFI_PopupMenu;

        private readonly IntPtr FFI_OpenFile;

        private readonly IntPtr FFI_EmailTo;

        private readonly IntPtr FFI_UploadTo;

        private readonly IntPtr FFI_GetPlatform;

        private readonly IntPtr FFI_GetLanguage;

        private readonly IntPtr FFI_DownloadFromURL;

        private readonly IntPtr FFI_PostRequestURL;

        private readonly IntPtr FFI_PutRequestURL;
    }
}