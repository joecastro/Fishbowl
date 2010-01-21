
namespace Standard
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.ComTypes;
    using System.Security.Permissions;
    using Microsoft.Win32.SafeHandles;

    // Some COM interfaces and Win32 structures are already declared in the framework.
    // Interesting ones to remember in System.Runtime.InteropServices.ComTypes are:
    using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;
    using IPersistFile = System.Runtime.InteropServices.ComTypes.IPersistFile;
    using IStream = System.Runtime.InteropServices.ComTypes.IStream;

    #region Native Values

    internal static class Win32Value
    {
        public const uint MAX_PATH = 260;
        public const uint INFOTIPSIZE = 1024;
        public const uint TRUE = 1;
        public const uint FALSE = 0;
        public const uint sizeof_WCHAR = 2;
        public const uint sizeof_CHAR = 1;
        public const uint sizeof_BOOL = 4;
    }

    /// <summary>
    /// BITMAPINFOHEADER Compression type.  BI_*.
    /// </summary>
    internal enum BI
    {
        RGB = 0,
    }
    
    /// <summary>
    /// For IWebBrowser2.  OLECMDEXECOPT_*
    /// </summary>
    internal enum OLECMDEXECOPT
    {
        DODEFAULT = 0,
        PROMPTUSER = 1,
        DONTPROMPTUSER = 2,
        SHOWHELP = 3
    }

    /// <summary>
    /// For IWebBrowser2.  OLECMDF_*
    /// </summary>
    internal enum OLECMDF
    {
        SUPPORTED = 1,
        ENABLED = 2,
        LATCHED = 4,
        NINCHED = 8,
        INVISIBLE = 16,
        DEFHIDEONCTXTMENU = 32
    }

    /// <summary>
    /// For IWebBrowser2.  OLECMDID_*
    /// </summary>
    internal enum OLECMDID
    {
        OPEN = 1,
        NEW = 2,
        SAVE = 3,
        SAVEAS = 4,
        SAVECOPYAS = 5,
        PRINT = 6,
        PRINTPREVIEW = 7,
        PAGESETUP = 8,
        SPELL = 9,
        PROPERTIES = 10,
        CUT = 11,
        COPY = 12,
        PASTE = 13,
        PASTESPECIAL = 14,
        UNDO = 15,
        REDO = 16,
        SELECTALL = 17,
        CLEARSELECTION = 18,
        ZOOM = 19,
        GETZOOMRANGE = 20,
        UPDATECOMMANDS = 21,
        REFRESH = 22,
        STOP = 23,
        HIDETOOLBARS = 24,
        SETPROGRESSMAX = 25,
        SETPROGRESSPOS = 26,
        SETPROGRESSTEXT = 27,
        SETTITLE = 28,
        SETDOWNLOADSTATE = 29,
        STOPDOWNLOAD = 30,
        ONTOOLBARACTIVATED = 31,
        FIND = 32,
        DELETE = 33,
        HTTPEQUIV = 34,
        HTTPEQUIV_DONE = 35,
        ENABLE_INTERACTION = 36,
        ONUNLOAD = 37,
        PROPERTYBAG2 = 38,
        PREREFRESH = 39,
        SHOWSCRIPTERROR = 40,
        SHOWMESSAGE = 41,
        SHOWFIND = 42,
        SHOWPAGESETUP = 43,
        SHOWPRINT = 44,
        CLOSE = 45,
        ALLOWUILESSSAVEAS = 46,
        DONTDOWNLOADCSS = 47,
        UPDATEPAGESTATUS = 48,
        PRINT2 = 49,
        PRINTPREVIEW2 = 50,
        SETPRINTTEMPLATE = 51,
        GETPRINTTEMPLATE = 52,
        PAGEACTIONBLOCKED = 55,
        PAGEACTIONUIQUERY = 56,
        FOCUSVIEWCONTROLS = 57,
        FOCUSVIEWCONTROLSQUERY = 58,
        SHOWPAGEACTIONMENU = 59
    }

    /// <summary>
    /// For IWebBrowser2.  READYSTATE_*
    /// </summary>
    enum READYSTATE
    {
        UNINITIALIZED = 0,
        LOADING = 1,
        LOADED = 2,
        INTERACTIVE = 3,
        COMPLETE = 4
    }

    /// <summary>
    /// DATAOBJ_GET_ITEM_FLAGS.  DOGIF_*.
    /// </summary>
    internal enum DOGIF
    {
        DEFAULT = 0x0000,
        TRAVERSE_LINK = 0x0001,    // if the item is a link get the target
        NO_HDROP = 0x0002,    // don't fallback and use CF_HDROP clipboard format
        NO_URL = 0x0004,    // don't fallback and use URL clipboard format
        ONLY_IF_ONE = 0x0008,    // only return the item if there is one item in the array
    }

    internal enum DWM_SIT
    {
        None,
        DISPLAYFRAME = 1,
    }

    [Flags]
    internal enum ErrorModes
    {
        /// <summary>Use the system default, which is to display all error dialog boxes.</summary>
        Default = 0x0,
        /// <summary>
        /// The system does not display the critical-error-handler message box. 
        /// Instead, the system sends the error to the calling process.
        /// </summary>
        FailCriticalErrors = 0x1,
        /// <summary>
        /// 64-bit Windows:  The system automatically fixes memory alignment faults and makes them 
        /// invisible to the application. It does this for the calling process and any descendant processes.
        /// After this value is set for a process, subsequent attempts to clear the value are ignored.
        /// </summary>
        NoGpFaultErrorBox = 0x2,
        /// <summary>
        /// The system does not display the general-protection-fault message box. 
        /// This flag should only be set by debugging applications that handle general 
        /// protection (GP) faults themselves with an exception handler.
        /// </summary>
        NoAlignmentFaultExcept = 0x4,
        /// <summary>
        /// The system does not display a message box when it fails to find a file. 
        /// Instead, the error is returned to the calling process.
        /// </summary>
        NoOpenFileErrorBox = 0x8000
    }

    /// <summary>
    /// Non-client hit test values, HT*
    /// </summary>
    internal enum HT
    {
        ERROR = -2,
        TRANSPARENT = -1,
        NOWHERE = 0,
        CLIENT = 1,
        CAPTION = 2,
        SYSMENU = 3,
        GROWBOX = 4,
        MENU = 5,
        HSCROLL = 6,
        VSCROLL = 7,
        MINBUTTON = 8,
        MAXBUTTON = 9,
        LEFT = 10,
        RIGHT = 11,
        TOP = 12,
        TOPLEFT = 13,
        TOPRIGHT = 14,
        BOTTOM = 15,
        BOTTOMLEFT = 16,
        BOTTOMRIGHT = 17,
        BORDER = 18,
        OBJECT = 19,
        CLOSE = 20,
        HELP = 21
    }

    /// <summary>
    /// GetClassLongPtr values, GCLP_*
    /// </summary>
    internal enum GCLP
    {
        HBRBACKGROUND = -10,
    }

    /// <summary>
    /// GetWindowLongPtr values, GWL_*
    /// </summary>
    internal enum GWL
    {
        WNDPROC = (-4),
        HINSTANCE = (-6),
        HWNDPARENT = (-8),
        STYLE = (-16),
        EXSTYLE = (-20),
        USERDATA = (-21),
        ID = (-12)
    }

    /// <summary>
    /// SystemMetrics.  SM_*
    /// </summary>
    internal enum SM
    {
        CXSCREEN = 0,
        CYSCREEN = 1,
        CXVSCROLL = 2,
        CYHSCROLL = 3,
        CYCAPTION = 4,
        CXBORDER = 5,
        CYBORDER = 6,
        CXFIXEDFRAME = 7,
        CYFIXEDFRAME = 8,
        CYVTHUMB = 9,
        CXHTHUMB = 10,
        CXICON = 11,
        CYICON = 12,
        CXCURSOR = 13,
        CYCURSOR = 14,
        CYMENU = 15,
        CXFULLSCREEN = 16,
        CYFULLSCREEN = 17,
        CYKANJIWINDOW = 18,
        MOUSEPRESENT = 19,
        CYVSCROLL = 20,
        CXHSCROLL = 21,
        DEBUG = 22,
        SWAPBUTTON = 23,
        CXMIN = 28,
        CYMIN = 29,
        CXSIZE = 30,
        CYSIZE = 31,
        CXFRAME = 32,
        CXSIZEFRAME = CXFRAME,
        CYFRAME = 33,
        CYSIZEFRAME = CYFRAME,
        CXMINTRACK = 34,
        CYMINTRACK = 35,
        CXDOUBLECLK = 36,
        CYDOUBLECLK = 37,
        CXICONSPACING = 38,
        CYICONSPACING = 39,
        MENUDROPALIGNMENT = 40,
        PENWINDOWS = 41,
        DBCSENABLED = 42,
        CMOUSEBUTTONS = 43,
        SECURE = 44,
        CXEDGE = 45,
        CYEDGE = 46,
        CXMINSPACING = 47,
        CYMINSPACING = 48,
        CXSMICON = 49,
        CYSMICON = 50,
        CYSMCAPTION = 51,
        CXSMSIZE = 52,
        CYSMSIZE = 53,
        CXMENUSIZE = 54,
        CYMENUSIZE = 55,
        ARRANGE = 56,
        CXMINIMIZED = 57,
        CYMINIMIZED = 58,
        CXMAXTRACK = 59,
        CYMAXTRACK = 60,
        CXMAXIMIZED = 61,
        CYMAXIMIZED = 62,
        NETWORK = 63,
        CLEANBOOT = 67,
        CXDRAG = 68,
        CYDRAG = 69,
        SHOWSOUNDS = 70,
        CXMENUCHECK = 71,
        CYMENUCHECK = 72,
        SLOWMACHINE = 73,
        MIDEASTENABLED = 74,
        MOUSEWHEELPRESENT = 75,
        XVIRTUALSCREEN = 76,
        YVIRTUALSCREEN = 77,
        CXVIRTUALSCREEN = 78,
        CYVIRTUALSCREEN = 79,
        CMONITORS = 80,
        SAMEDISPLAYFORMAT = 81,
        IMMENABLED = 82,
        CXFOCUSBORDER = 83,
        CYFOCUSBORDER = 84,
        TABLETPC = 86,
        MEDIACENTER = 87,
        REMOTESESSION = 0x1000,
        REMOTECONTROL = 0x2001,
    }

    /// <summary>
    /// SystemParameterInfo values, SPI_*
    /// </summary>
    internal enum SPI
    {
        SETDESKWALLPAPER = 20,
        GETNONCLIENTMETRICS = 41,
    }

    /// <summary>
    /// SystemParameterInfo flag values, SPIF_*
    /// </summary>
    [Flags]
    internal enum SPIF
    {
        None = 0,
        UPDATEINIFILE = 0x01,
        SENDWININICHANGE = 0x02,
    }

    /// <summary>
    /// CS_*
    /// </summary>
    [Flags]
    internal enum CS : uint
    {
        VREDRAW = 0x0001,
        HREDRAW = 0x0002,
        DBLCLKS = 0x0008,
        OWNDC = 0x0020,
        CLASSDC = 0x0040,
        PARENTDC = 0x0080,
        NOCLOSE = 0x0200,
        SAVEBITS = 0x0800,
        BYTEALIGNCLIENT = 0x1000,
        BYTEALIGNWINDOW = 0x2000,
        GLOBALCLASS = 0x4000,
        IME = 0x00010000,
        DROPSHADOW = 0x00020000
    }

    /// <summary>
    /// WindowStyle values, WS_*
    /// </summary>
    [Flags]
    internal enum WS : uint
    {
        OVERLAPPED = 0x00000000,
        POPUP = 0x80000000,
        CHILD = 0x40000000,
        MINIMIZE = 0x20000000,
        VISIBLE = 0x10000000,
        DISABLED = 0x08000000,
        CLIPSIBLINGS = 0x04000000,
        CLIPCHILDREN = 0x02000000,
        MAXIMIZE = 0x01000000,
        BORDER = 0x00800000,
        DLGFRAME = 0x00400000,
        VSCROLL = 0x00200000,
        HSCROLL = 0x00100000,
        SYSMENU = 0x00080000,
        THICKFRAME = 0x00040000,
        GROUP = 0x00020000,
        TABSTOP = 0x00010000,

        MINIMIZEBOX = 0x00020000,
        MAXIMIZEBOX = 0x00010000,

        CAPTION = BORDER | DLGFRAME,
        TILED = OVERLAPPED,
        ICONIC = MINIMIZE,
        SIZEBOX = THICKFRAME,
        TILEDWINDOW = OVERLAPPEDWINDOW,

        OVERLAPPEDWINDOW = OVERLAPPED | CAPTION | SYSMENU | THICKFRAME | MINIMIZEBOX | MAXIMIZEBOX,
        POPUPWINDOW = POPUP | BORDER | SYSMENU,
        CHILDWINDOW = CHILD,
    }

    /// <summary>
    /// Window message values, WM_*
    /// </summary>
    internal enum WM
    {
        NULL = 0x0000,
        CREATE = 0x0001,
        DESTROY = 0x0002,
        MOVE = 0x0003,
        SIZE = 0x0005,
        ACTIVATE = 0x0006,
        SETFOCUS = 0x0007,
        KILLFOCUS = 0x0008,
        ENABLE = 0x000A,
        SETREDRAW = 0x000B,
        SETTEXT = 0x000C,
        GETTEXT = 0x000D,
        GETTEXTLENGTH = 0x000E,
        PAINT = 0x000F,
        CLOSE = 0x0010,
        QUERYENDSESSION = 0x0011,
        QUIT = 0x0012,
        QUERYOPEN = 0x0013,
        ERASEBKGND = 0x0014,
        SYSCOLORCHANGE = 0x0015,
        SHOWWINDOW = 0x0018,
        ACTIVATEAPP = 0x001C,
        SETCURSOR = 0x0020,
        MOUSEACTIVATE = 0x0021,
        CHILDACTIVATE = 0x0022,
        QUEUESYNC = 0x0023,
        GETMINMAXINFO = 0x0024,

        WINDOWPOSCHANGING = 0x0046,
        WINDOWPOSCHANGED = 0x0047,

        CONTEXTMENU = 0x007B,
        STYLECHANGING = 0x007C,
        STYLECHANGED = 0x007D,
        DISPLAYCHANGE = 0x007E,
        GETICON = 0x007F,
        SETICON = 0x0080,
        NCCREATE = 0x0081,
        NCDESTROY = 0x0082,
        NCCALCSIZE = 0x0083,
        NCHITTEST = 0x0084,
        NCPAINT = 0x0085,
        NCACTIVATE = 0x0086,
        GETDLGCODE = 0x0087,
        SYNCPAINT = 0x0088,
        NCMOUSEMOVE = 0x00A0,
        NCLBUTTONDOWN = 0x00A1,
        NCLBUTTONUP = 0x00A2,
        NCLBUTTONDBLCLK = 0x00A3,
        NCRBUTTONDOWN = 0x00A4,
        NCRBUTTONUP = 0x00A5,
        NCRBUTTONDBLCLK = 0x00A6,
        NCMBUTTONDOWN = 0x00A7,
        NCMBUTTONUP = 0x00A8,
        NCMBUTTONDBLCLK = 0x00A9,

        SYSKEYDOWN = 0x0104,
        SYSKEYUP = 0x0105,
        SYSCHAR = 0x0106,
        SYSDEADCHAR = 0x0107,
        COMMAND = 0x0111,
        SYSCOMMAND = 0x0112,

        MOUSEMOVE = 0x0200,
        LBUTTONDOWN = 0x0201,
        LBUTTONUP = 0x0202,
        LBUTTONDBLCLK = 0x0203,
        RBUTTONDOWN = 0x0204,
        RBUTTONUP = 0x0205,
        RBUTTONDBLCLK = 0x0206,
        MBUTTONDOWN = 0x0207,
        MBUTTONUP = 0x0208,
        MBUTTONDBLCLK = 0x0209,
        MOUSEWHEEL = 0x020A,
        XBUTTONDOWN = 0x020B,
        XBUTTONUP = 0x020C,
        XBUTTONDBLCLK = 0x020D,
        MOUSEHWHEEL = 0x020E,
        PARENTNOTIFY = 0x0210,

        CAPTURECHANGED = 0x0215,

        ENTERSIZEMOVE = 0x0231,
        EXITSIZEMOVE = 0x0232,

        IME_SETCONTEXT = 0x0281,
        IME_NOTIFY = 0x0282,
        IME_CONTROL = 0x0283,
        IME_COMPOSITIONFULL = 0x0284,
        IME_SELECT = 0x0285,
        IME_CHAR = 0x0286,
        IME_REQUEST = 0x0288,
        IME_KEYDOWN = 0x0290,
        IME_KEYUP = 0x0291,

        NCMOUSELEAVE = 0x02A2,

        DWMCOMPOSITIONCHANGED = 0x031E,
        DWMNCRENDERINGCHANGED = 0x031F,
        DWMCOLORIZATIONCOLORCHANGED = 0x0320,
        DWMWINDOWMAXIMIZEDCHANGE = 0x0321,

        #region Windows 7
        DWMSENDICONICTHUMBNAIL = 0x0323,
        DWMSENDICONICLIVEPREVIEWBITMAP = 0x0326,
        #endregion

        USER = 0x0400,

        // This is the hard-coded message value used by WinForms for Shell_NotifyIcon.
        // It's relatively safe to reuse.
        TRAYMOUSEMESSAGE = 0x800, //WM_USER + 1024
        APP = 0x8000,
    }

    /// <summary>
    /// Window style extended values, WS_EX_*
    /// </summary>
    [Flags]
    internal enum WS_EX : uint
    {
        None = 0,
        DLGMODALFRAME = 0x00000001,
        NOPARENTNOTIFY = 0x00000004,
        TOPMOST = 0x00000008,
        ACCEPTFILES = 0x00000010,
        TRANSPARENT = 0x00000020,
        MDICHILD = 0x00000040,
        TOOLWINDOW = 0x00000080,
        WINDOWEDGE = 0x00000100,
        CLIENTEDGE = 0x00000200,
        CONTEXTHELP = 0x00000400,
        RIGHT = 0x00001000,
        LEFT = 0x00000000,
        RTLREADING = 0x00002000,
        LTRREADING = 0x00000000,
        LEFTSCROLLBAR = 0x00004000,
        RIGHTSCROLLBAR = 0x00000000,
        CONTROLPARENT = 0x00010000,
        STATICEDGE = 0x00020000,
        APPWINDOW = 0x00040000,
        LAYERED = 0x00080000,
        NOINHERITLAYOUT = 0x00100000, // Disable inheritence of mirroring by children
        LAYOUTRTL = 0x00400000, // Right to left mirroring
        COMPOSITED = 0x02000000,
        NOACTIVATE = 0x08000000,
        OVERLAPPEDWINDOW = (WINDOWEDGE | CLIENTEDGE),
        PALETTEWINDOW = (WINDOWEDGE | TOOLWINDOW | TOPMOST),
    }

    /// <summary>
    /// GetDeviceCaps nIndex values.
    /// </summary>
    internal enum DeviceCap
    {
        /// <summary>Number of bits per pixel
        /// </summary>
        BITSPIXEL = 12,
        /// <summary>
        /// Number of planes
        /// </summary>
        PLANES = 14,
        /// <summary>
        /// Logical pixels inch in X
        /// </summary>
        LOGPIXELSX = 88,
        /// <summary>
        /// Logical pixels inch in Y
        /// </summary>
        LOGPIXELSY = 90,
    }

    internal enum FO : int
    {
        MOVE = 0x0001,
        COPY = 0x0002,
        DELETE = 0x0003,
        RENAME = 0x0004,
    }

    /// <summary>
    /// "FILEOP_FLAGS", FOF_*.
    /// </summary>
    internal enum FOF : ushort
    {
        MULTIDESTFILES = 0x0001,
        CONFIRMMOUSE = 0x0002,
        SILENT = 0x0004,
        RENAMEONCOLLISION = 0x0008,
        NOCONFIRMATION = 0x0010,
        WANTMAPPINGHANDLE = 0x0020,
        ALLOWUNDO = 0x0040,
        FILESONLY = 0x0080,
        SIMPLEPROGRESS = 0x0100,
        NOCONFIRMMKDIR = 0x0200,
        NOERRORUI = 0x0400,
        NOCOPYSECURITYATTRIBS = 0x0800,
        NORECURSION = 0x1000,
        NO_CONNECTED_ELEMENTS = 0x2000,
        WANTNUKEWARNING = 0x4000,
        NORECURSEREPARSE = 0x8000,
    }

    /// <summary>
    /// EnableMenuItem uEnable values, MF_*
    /// </summary>
    [Flags]
    internal enum MF : uint
    {
        /// <summary>
        /// Possible return value for EnableMenuItem
        /// </summary>
        DOES_NOT_EXIST = unchecked((uint)-1),
        ENABLED = 0,
        BYCOMMAND = 0,
        GRAYED = 1,
        DISABLED = 2,
    }

    /// <summary>Specifies the type of visual style attribute to set on a window.</summary>
    internal enum WINDOWTHEMEATTRIBUTETYPE : uint
    {
        /// <summary>Non-client area window attributes will be set.</summary>
        WTA_NONCLIENT = 1,
    }

    /// <summary>
    /// DWMFLIP3DWINDOWPOLICY.  DWMFLIP3D_*
    /// </summary>
    internal enum DWMFLIP3D 
    {
        DEFAULT,
        EXCLUDEBELOW,
        EXCLUDEABOVE,
        //LAST
    }

    /// <summary>
    /// DWMNCRENDERINGPOLICY. DWMNCRP_*
    /// </summary>
    internal enum DWMNCRP
    {
        USEWINDOWSTYLE,
        DISABLED,
        ENABLED,
        //LAST
    }

    /// <summary>
    /// DWMWINDOWATTRIBUTE.  DWMWA_*
    /// </summary>
    internal enum DWMWA
    {
        NCRENDERING_ENABLED = 1,
        NCRENDERING_POLICY,
        TRANSITIONS_FORCEDISABLED,
        ALLOW_NCPAINT,
        CAPTION_BUTTON_BOUNDS,
        NONCLIENT_RTL_LAYOUT,
        FORCE_ICONIC_REPRESENTATION,
        FLIP3D_POLICY,
        EXTENDED_FRAME_BOUNDS,

        // New to Windows 7:

        HAS_ICONIC_BITMAP,
        DISALLOW_PEEK,
        EXCLUDED_FROM_PEEK,

        // LAST
    }

    /// <summary>
    /// WindowThemeNonClientAttributes
    /// </summary>
    [Flags]
    internal enum WTNCA : uint
    {
        /// <summary>Prevents the window caption from being drawn.</summary>
        NODRAWCAPTION = 0x00000001,
        /// <summary>Prevents the system icon from being drawn.</summary>
        NODRAWICON = 0x00000002,
        /// <summary>Prevents the system icon menu from appearing.</summary>
        NOSYSMENU = 0x00000004,
        /// <summary>Prevents mirroring of the question mark, even in right-to-left (RTL) layout.</summary>
        NOMIRRORHELP = 0x00000008,
        /// <summary> A mask that contains all the valid bits.</summary>
        VALIDBITS = NODRAWCAPTION | NODRAWICON | NOMIRRORHELP | NOSYSMENU,
    }

    /// <summary>
    /// SetWindowPos options
    /// </summary>
    [Flags]
    internal enum SWP
    {
        ASYNCWINDOWPOS = 0x4000,
        DEFERERASE = 0x2000,
        DRAWFRAME = 0x0020,
        FRAMECHANGED = 0x0020,
        HIDEWINDOW = 0x0080,
        NOACTIVATE = 0x0010,
        NOCOPYBITS = 0x0100,
        NOMOVE = 0x0002,
        NOOWNERZORDER = 0x0200,
        NOREDRAW = 0x0008,
        NOREPOSITION = 0x0200,
        NOSENDCHANGING = 0x0400,
        NOSIZE = 0x0001,
        NOZORDER = 0x0004,
        SHOWWINDOW = 0x0040,
    }

    /// <summary>
    /// ShowWindow options
    /// </summary>
    internal enum SW
    {
        HIDE = 0,
        SHOWNORMAL = 1,
        NORMAL = 1,
        SHOWMINIMIZED = 2,
        SHOWMAXIMIZED = 3,
        MAXIMIZE = 3,
        SHOWNOACTIVATE = 4,
        SHOW = 5,
        MINIMIZE = 6,
        SHOWMINNOACTIVE = 7,
        SHOWNA = 8,
        RESTORE = 9,
        SHOWDEFAULT = 10,
        FORCEMINIMIZE = 11,
    }

    internal enum SC
    {
        SIZE = 0xF000,
        MOVE = 0xF010,
        MINIMIZE = 0xF020,
        MAXIMIZE = 0xF030,
        NEXTWINDOW = 0xF040,
        PREVWINDOW = 0xF050,
        CLOSE = 0xF060,
        VSCROLL = 0xF070,
        HSCROLL = 0xF080,
        MOUSEMENU = 0xF090,
        KEYMENU = 0xF100,
        ARRANGE = 0xF110,
        RESTORE = 0xF120,
        TASKLIST = 0xF130,
        SCREENSAVE = 0xF140,
        HOTKEY = 0xF150,
        DEFAULT = 0xF160,
        MONITORPOWER = 0xF170,
        CONTEXTHELP = 0xF180,
        SEPARATOR = 0xF00F,
        /// <summary>
        /// SCF_ISSECURE
        /// </summary>
        F_ISSECURE = 0x00000001,
        ICON = MINIMIZE,
        ZOOM = MAXIMIZE,
    }

    /// <summary>
    /// GDI+ Status codes
    /// </summary>
    internal enum Status
    {
        Ok = 0,
        GenericError = 1,
        InvalidParameter = 2,
        OutOfMemory = 3,
        ObjectBusy = 4,
        InsufficientBuffer = 5,
        NotImplemented = 6,
        Win32Error = 7,
        WrongState = 8,
        Aborted = 9,
        FileNotFound = 10,
        ValueOverflow = 11,
        AccessDenied = 12,
        UnknownImageFormat = 13,
        FontFamilyNotFound = 14,
        FontStyleNotFound = 15,
        NotTrueTypeFont = 16,
        UnsupportedGdiplusVersion = 17,
        GdiplusNotInitialized = 18,
        PropertyNotFound = 19,
        PropertyNotSupported = 20,
        ProfileNotFound = 21,
    }

    internal enum MOUSEEVENTF : int
    {
        //mouse event constants
        LEFTDOWN = 2,
        LEFTUP = 4
    }

    /// <summary>
    /// MSGFLT_*.  New in Vista.  Realiased in Windows 7.
    /// </summary>
    internal enum MSGFLT
    {
        // Win7 versions of this enum:
        RESET = 0,
        ALLOW = 1,
        DISALLOW = 2,

        // Vista versions of this enum:
        // ADD = 1,
        // REMOVE = 2,
    }

    internal enum MSGFLTINFO
    {
        NONE = 0,
        ALREADYALLOWED_FORWND = 1,
        ALREADYDISALLOWED_FORWND = 2,
        ALLOWED_HIGHER = 3,
    }

    internal enum INPUT_TYPE : uint
    {
        MOUSE = 0,
    }

    /// <summary>
    /// Shell_NotifyIcon messages.  NIM_*
    /// </summary>
    internal enum NIM : uint
    {
        ADD = 0,
        MODIFY = 1,
        DELETE = 2,
        SETFOCUS = 3,
        SETVERSION = 4,
    }

    /// <summary>
    /// SHAddToRecentDocuments flags.  SHARD_*
    /// </summary>
    internal enum SHARD
    {
        PIDL = 0x00000001,
        PATHA = 0x00000002,
        PATHW = 0x00000003,
        APPIDINFO = 0x00000004, // indicates the data type is a pointer to a SHARDAPPIDINFO structure
        APPIDINFOIDLIST = 0x00000005, // indicates the data type is a pointer to a SHARDAPPIDINFOIDLIST structure
        LINK = 0x00000006, // indicates the data type is a pointer to an IShellLink instance
        APPIDINFOLINK = 0x00000007, // indicates the data type is a pointer to a SHARDAPPIDINFOLINK structure 
    }

    [Flags]
    enum SLGP
    {
        SHORTPATH = 0x1,
        UNCPRIORITY = 0x2,
        RAWPATH = 0x4
    }

    /// <summary>
    /// Shell_NotifyIcon flags.  NIF_*
    /// </summary>
    [Flags]
    internal enum NIF : uint
    {
        MESSAGE = 0x0001,
        ICON = 0x0002,
        TIP = 0x0004,
        STATE = 0x0008,
        INFO = 0x0010,
        GUID = 0x0020,

        /// <summary>
        /// Vista only.
        /// </summary>
        REALTIME = 0x0040,
        /// <summary>
        /// Vista only.
        /// </summary>
        SHOWTIP = 0x0080,

        XP_MASK = MESSAGE | ICON | STATE | INFO | GUID,
        VISTA_MASK = XP_MASK | REALTIME | SHOWTIP,
    }

    /// <summary>
    /// Shell_NotifyIcon info flags.  NIIF_*
    /// </summary>
    public enum NIIF
    {
        NONE = 0x00000000,
        INFO = 0x00000001,
        WARNING = 0x00000002,
        ERROR = 0x00000003,
        /// <summary>XP SP2 and later.</summary>
        USER = 0x00000004,
        /// <summary>XP and later.</summary>
        NOSOUND = 0x00000010,
        /// <summary>Vista and later.</summary>
        LARGE_ICON = 0x00000020,
        /// <summary>Windows 7 and later</summary>
        NIIF_RESPECT_QUIET_TIME = 0x00000080,
        /// <summary>XP and later.  Native version called NIIF_ICON_MASK.</summary>
        XP_ICON_MASK = 0x0000000F,
    }

    /// <summary>
    /// AC_*
    /// </summary>
    internal enum AC : byte
    {
        SRC_OVER = 0,
        SRC_ALPHA = 1,
    }

    internal enum ULW
    {
        ALPHA = 2,
        COLORKEY = 1,
        OPAQUE = 4,
    }

    #endregion

    #region SafeHandles

    internal sealed class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        private SafeFindHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.FindClose(handle);
        }
    }

    internal sealed class SafeDC : SafeHandleZeroOrMinusOneIsInvalid
    {
        private static class NativeMethods
        {
            [DllImport("user32.dll")]
            public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

            [DllImport("user32.dll")]
            public static extern SafeDC GetDC(IntPtr hwnd);

            // Weird legacy function, documentation is unclear about how to use it...
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
            public static extern SafeDC CreateDC([MarshalAs(UnmanagedType.LPWStr)] string lpszDriver, [MarshalAs(UnmanagedType.LPWStr)] string lpszDevice, IntPtr lpszOutput, IntPtr lpInitData);

            [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError=true)]
            public static extern SafeDC CreateCompatibleDC(IntPtr hdc);

            [DllImport("gdi32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeleteDC(IntPtr hdc);
        }

        private IntPtr? _hwnd;
        private bool _created;

        public IntPtr Hwnd
        {
            set
            {
                Assert.NullableIsNull(_hwnd);
                _hwnd = value;
            }
        }

        private SafeDC() : base(true) { }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        protected override bool ReleaseHandle()
        {
            if (_created)
            {
                return NativeMethods.DeleteDC(handle);
            }

            if (!_hwnd.HasValue || _hwnd.Value == IntPtr.Zero)
            {
                return true;
            }

            return NativeMethods.ReleaseDC(_hwnd.Value, handle) == 1;
        }

        public static SafeDC CreateDC(string deviceName)
        {
            SafeDC dc = null;
            try
            {
                // Should this really be on the driver parameter?
                dc = NativeMethods.CreateDC(deviceName, null, IntPtr.Zero, IntPtr.Zero);
            }
            finally
            {
                if (dc != null)
                {
                    dc._created = true;
                }
            }

            if (dc.IsInvalid)
            {
                throw new SystemException("Unable to create a device context from the specified device information.");
            }

            return dc;
        }

        public static SafeDC CreateCompatibleDC(SafeDC hdc)
        {
            SafeDC dc = null;
            try
            {
                IntPtr hPtr = IntPtr.Zero;
                if (hdc != null)
                {
                    hPtr = hdc.handle;
                }
                dc = NativeMethods.CreateCompatibleDC(hPtr);
                if (dc == null)
                {
                    HRESULT.ThrowLastError();
                }
            }
            finally
            {
                if (dc != null)
                {
                    dc._created = true;
                }
            }

            if (dc.IsInvalid)
            {
                throw new SystemException("Unable to create a device context from the specified device information.");
            }

            return dc;
        }

        public static SafeDC GetDC(IntPtr hwnd)
        {
            SafeDC dc = null;
            try
            {
                dc = NativeMethods.GetDC(hwnd);
            }
            finally
            {
                if (dc != null)
                {
                    dc.Hwnd = hwnd;
                }
            }

            if (dc.IsInvalid)
            {
                // GetDC does not set the last error...
                HRESULT.E_FAIL.ThrowIfFailed();
            }

            return dc;
        }

        public static SafeDC GetDesktop()
        {
            return GetDC(IntPtr.Zero);
        }

        public static SafeDC WrapDC(IntPtr hdc)
        {
            // This won't actually get released by the class, but it allows an IntPtr to be converted for signatures.
            return new SafeDC
            {
                handle = hdc,
                _created = false,
                _hwnd = IntPtr.Zero,
            };
        }
    }

    internal sealed class SafeHBITMAP : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeHBITMAP() : base(true) { }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        protected override bool ReleaseHandle()
        {
            return NativeMethods.DeleteObject(handle);
        }
    }

    internal sealed class SafeGdiplusStartupToken : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeGdiplusStartupToken() : base(true) { }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        protected override bool ReleaseHandle()
        {
            Status s = NativeMethods.GdiplusShutdown(this.handle);
            return s == Status.Ok;
        }

        public static SafeGdiplusStartupToken Startup()
        {
            SafeGdiplusStartupToken safeHandle = new SafeGdiplusStartupToken();
            IntPtr unsafeHandle;
            StartupOutput output;
            Status s = NativeMethods.GdiplusStartup(out unsafeHandle, new StartupInput(), out output);
            if (s == Status.Ok)
            {
                safeHandle.handle = unsafeHandle;
                return safeHandle;
            }
            throw new Exception("Unable to initialize GDI+");
        }
    }

    internal sealed class SafeConnectionPointCookie : SafeHandleZeroOrMinusOneIsInvalid
    {
        private IConnectionPoint _cp;
        // handle holds the cookie value.

        public SafeConnectionPointCookie(IConnectionPointContainer target, object sink, Guid eventId)
            : base(true)
        {
            Verify.IsNotNull(target, "target");
            Verify.IsNotNull(sink, "sink");
            Verify.IsNotDefault(eventId, "eventId");

            handle = IntPtr.Zero;

            IConnectionPoint cp = null;
            try
            {
                int dwCookie;
                target.FindConnectionPoint(ref eventId, out cp);
                cp.Advise(sink, out dwCookie);
                if (dwCookie == 0)
                {
                    throw new InvalidOperationException("IConnectionPoint::Advise returned an invalid cookie.");
                }
                handle = new IntPtr(dwCookie);
                _cp = cp;
                cp = null;
            }
            finally
            {
                Utility.SafeRelease(ref cp);
            }
        }

        public void Disconnect()
        {
            ReleaseHandle();
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        protected override bool ReleaseHandle()
        {
            try
            {
                if (!this.IsInvalid)
                {
                    int dwCookie = handle.ToInt32();
                    handle = IntPtr.Zero;

                    Assert.IsNotNull(_cp);
                    try
                    {
                        _cp.Unadvise(dwCookie);
                    }
                    finally
                    {
                        Utility.SafeRelease(ref _cp);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    #endregion

    #region Native Types

    [StructLayout(LayoutKind.Sequential)]
    internal struct BLENDFUNCTION
    {
        // Must be AC_SRC_OVER
        public AC BlendOp;
        // Must be 0.
        public byte BlendFlags;
        // Alpha transparency between 0 (transparent) - 255 (opaque)
        public byte SourceConstantAlpha;
        // Must be AC_SRC_ALPHA
        public AC AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RGBQUAD
    {
        public byte rgbBlue;
        public byte rgbGreen;
        public byte rgbRed;
        public byte rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack=2)]
    internal struct BITMAPINFOHEADER 
    { 
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public BI biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFO
    { 
        public BITMAPINFOHEADER bmiHeader; 
        public RGBQUAD bmiColors;
    }

    // Win7 only.
    [StructLayout(LayoutKind.Sequential)]
    internal struct CHANGEFILTERSTRUCT
    {
        public uint cbSize;
        public MSGFLTINFO ExtStatus;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    internal struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.U4)]
        public FO wFunc;
        // double-null terminated arrays of LPWSTRS
        public string pFrom;
        public string pTo;
        [MarshalAs(UnmanagedType.U2)]
        public FOF fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal class NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public NIF uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public char[] szTip = new char[128];
        /// <summary>
        /// The state of the icon.  There are two flags that can be set independently.
        /// NIS_HIDDEN = 1.  The icon is hidden.
        /// NIS_SHAREDICON = 2.  The icon is shared.
        /// </summary>
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public char[] szInfo = new char[256];
        // Prior to Vista this was a union of uTimeout and uVersion.  As of Vista, uTimeout has been deprecated.
        public uint uVersion;  // Used with Shell_NotifyIcon flag NIM_SETVERSION.
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public char[] szInfoTitle = new char[64];
        public uint dwInfoFlags;
        public Guid guidItem;
        // Vista only
        IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal class PROPVARIANT : IDisposable
    {
        private static class NativeMethods
        {
            [DllImport("ole32.dll")]
            internal static extern int PropVariantClear(PROPVARIANT pvar);
        }

        [FieldOffset(0)]
        private ushort vt;
        [FieldOffset(8)]
        private IntPtr pointerVal;
        [FieldOffset(8)]
        private byte byteVal;
        [FieldOffset(8)]
        private long longVal;
        [FieldOffset(8)]
        private short boolVal;

        public VarEnum VarType
        {
            get { return (VarEnum)vt; }
        }

        // Right now only using this for strings.
        public string GetValue()
        {
            if (vt == (ushort)VarEnum.VT_LPWSTR)
            {
                return Marshal.PtrToStringUni(pointerVal);
            }

            return null;
        }

        public void SetValue(bool f)
        {
            Clear();
            vt = (ushort)VarEnum.VT_BOOL;
            boolVal = (short)(f ? -1 : 0);
        }

        public void SetValue(string val)
        {
            Clear();
            vt = (ushort)VarEnum.VT_LPWSTR;
            pointerVal = Marshal.StringToCoTaskMemUni(val);
        }

        public void Clear()
        {
            NativeMethods.PropVariantClear(this);
        }

        #region IDisposable Pattern

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~PROPVARIANT()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            Clear();
        }

        #endregion
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal class SHARDAPPIDINFO
    {
        [MarshalAs(UnmanagedType.Interface)]
        object psi;    // The namespace location of the the item that should be added to the recent docs folder.
        [MarshalAs(UnmanagedType.LPWStr)]
        string pszAppID;  // The id of the application that should be associated with this recent doc.
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal class SHARDAPPIDINFOIDLIST
    {
        /// <summary>The idlist for the shell item that should be added to the recent docs folder.</summary>
        IntPtr pidl;
        /// <summary>The id of the application that should be associated with this recent doc.</summary>
        [MarshalAs(UnmanagedType.LPWStr)]
        string pszAppID;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal class SHARDAPPIDINFOLINK
    {
        IntPtr psl;     // An IShellLink instance that when launched opens a recently used item in the specified 
        // application. This link is not added to the recent docs folder, but will be added to the
        // specified application's destination list.
        [MarshalAs(UnmanagedType.LPWStr)]
        string pszAppID;  // The id of the application that should be associated with this recent doc.
    }


    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct LOGFONT
    {
        public int lfHeight;
        public int lfWidth;
        public int lfEscapement;
        public int lfOrientation;
        public int lfWeight;
        public byte lfItalic;
        public byte lfUnderline;
        public byte lfStrikeOut;
        public byte lfCharSet;
        public byte lfOutPrecision;
        public byte lfClipPrecision;
        public byte lfQuality;
        public byte lfPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string lfFaceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NONCLIENTMETRICS
    {
        public int cbSize;
        public int iBorderWidth;
        public int iScrollWidth;
        public int iScrollHeight;
        public int iCaptionWidth;
        public int iCaptionHeight;
        public LOGFONT lfCaptionFont;
        public int iSmCaptionWidth;
        public int iSmCaptionHeight;
        public LOGFONT lfSmCaptionFont;
        public int iMenuWidth;
        public int iMenuHeight;
        public LOGFONT lfMenuFont;
        public LOGFONT lfStatusFont;
        public LOGFONT lfMessageFont;
        // Vista only
        public int iPaddedBorderWidth;

        public static NONCLIENTMETRICS VistaMetricsStruct
        {
            get
            {
                var ncm = new NONCLIENTMETRICS();
                ncm.cbSize = Marshal.SizeOf(typeof(NONCLIENTMETRICS));
                return ncm;
            }
        }

        public static NONCLIENTMETRICS XPMetricsStruct
        {
            get
            {
                var ncm = new NONCLIENTMETRICS();
                // Account for the missing iPaddedBorderWidth
                ncm.cbSize = Marshal.SizeOf(typeof(NONCLIENTMETRICS)) - sizeof(int);
                return ncm;
            }
        }
    }

    /// <summary>Defines options that are used to set window visual style attributes.</summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct WTA_OPTIONS
    {
        // public static readonly uint Size = (uint)Marshal.SizeOf(typeof(WTA_OPTIONS));
        public const uint Size = 8;

        /// <summary>
        /// A combination of flags that modify window visual style attributes.
        /// Can be a combination of the WTNCA constants.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Justification = "Used by native code.")]
        [FieldOffset(0)]
        public WTNCA dwFlags;

        /// <summary>
        /// A bitmask that describes how the values specified in dwFlags should be applied.
        /// If the bit corresponding to a value in dwFlags is 0, that flag will be removed.
        /// If the bit is 1, the flag will be added.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Justification = "Used by native code.")]
        [FieldOffset(4)]
        public WTNCA dwMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MARGINS
    {
        /// <summary>Width of left border that retains its size.</summary>
        public int cxLeftWidth;
        /// <summary>Width of right border that retains its size.</summary>
        public int cxRightWidth;
        /// <summary>Height of top border that retains its size.</summary>
        public int cyTopHeight;
        /// <summary>Height of bottom border that retains its size.</summary>
        public int cyBottomHeight;
    };

    [StructLayout(LayoutKind.Sequential)]
    internal class MONITORINFO
    {
        public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal class RefPOINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        private int _left;
        private int _top;
        private int _right;
        private int _bottom;

        public void Offset(int dx, int dy)
        {
            _left += dx;
            _top += dy;
            _right += dx;
            _bottom += dy;
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public int Left
        {
            get { return _left; }
            set { _left = value; }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public int Right
        {
            get { return _right; }
            set { _right = value; }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public int Top
        {
            get { return _top; }
            set { _top = value; }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public int Bottom
        {
            get { return _bottom; }
            set { _bottom = value; }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public int Width
        {
            get { return _right - _left; }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public int Height
        {
            get { return _bottom - _top; }
        }

        public POINT Position
        {
            get { return new POINT { x = _left, y = _top }; }
        }

        public SIZE Size
        {
            get { return new SIZE { cx = Width, cy = Height }; }
        }

    }

    [StructLayout(LayoutKind.Sequential)]
    internal class RefRECT
    {
        private int _left;
        private int _top;
        private int _right;
        private int _bottom;

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public RefRECT(int left, int top, int right, int bottom)
        {
            _left = left;
            _top = top;
            _right = right;
            _bottom = bottom;
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public int Width
        {
            get { return _right - _left; }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public int Height
        {
            get { return _bottom - _top; }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public int Left
        {
            get { return _left; }
            set { _left = value; }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public int Right
        {
            get { return _right; }
            set { _right = value; }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public int Top
        {
            get { return _top; }
            set { _top = value; }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public int Bottom
        {
            get { return _bottom; }
            set { _bottom = value; }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public void Offset(int dx, int dy)
        {
            _left += dx;
            _top += dy;
            _right += dx;
            _bottom += dy;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SIZE
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct StartupOutput
    {
        public IntPtr hook;
        public IntPtr unhook;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal class StartupInput
    {
        public int GdiplusVersion = 1;
        public IntPtr DebugEventCallback;
        public bool SuppressBackgroundThread;
        public bool SuppressExternalCodecs;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    [BestFitMapping(false)]
    internal class WIN32_FIND_DATAW
    {
        public FileAttributes dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public int nFileSizeHigh;
        public int nFileSizeLow;
        public int dwReserved0;
        public int dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal class WINDOWPLACEMENT
    {
        public int length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
        public int flags;
        public SW showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public int flags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    internal struct WNDCLASSEX
    {
        public int cbSize;
        public CS style;
        public WndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public int mouseData;
        public int dwFlags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    };

    #endregion

    #region Interfaces

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid(IID.ServiceProvider)]
    internal interface IServiceProvider
    {
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object QueryService(ref Guid guidService, ref Guid riid);
    }

    #endregion

    /// <summary>Delegate declaration that matches native WndProc signatures.</summary>
    internal delegate IntPtr WndProc(IntPtr hwnd, WM uMsg, IntPtr wParam, IntPtr lParam);

    /// <summary>Delegate declaration that matches managed WndProc signatures.</summary>
    internal delegate IntPtr MessageHandler(WM uMsg, IntPtr wParam, IntPtr lParam, out bool handled);

    // Some native methods are shimmed through public versions that handle converting failures into thrown exceptions.
    internal static class NativeMethods
    {
        [DllImport("user32.dll", EntryPoint="ChangeWindowMessageFilter", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _ChangeWindowMessageFilter(WM message, MSGFLT dwFlag);

        [DllImport("user32.dll", EntryPoint = "ChangeWindowMessageFilterEx", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _ChangeWindowMessageFilterEx(IntPtr hwnd, WM message, MSGFLT action, [In, Out, Optional] ref CHANGEFILTERSTRUCT pChangeFilterStruct);

        // Note that processes at or below SECURITY_MANDATORY_LOW_RID are not allowed to change the message filter.
        // If those processes call this function, it will fail and generate the extended error code, ERROR_ACCESS_DENIED.
        public static MSGFLTINFO ChangeWindowMessageFilterEx(IntPtr hwnd, WM message, MSGFLT action)
        {
            bool ret;

            // This origins of this API were added for Vista.  The Ex version was added for Windows 7.
            // If we're not on either, then this message filter isolation doesn't exist.
            if (!Utility.IsOSVistaOrNewer)
            {
                return MSGFLTINFO.NONE;
            }

            // If we're on Vista rather than Win7 then we can't use the Ex version of this function.
            // The Ex version is preferred if possible because this results in process-wide modifications of the filter
            // and is deprecated as of Win7.
            if (!Utility.IsOSWindows7OrNewer)
            {
                // Note that the Win7 MSGFLT_ALLOW/DISALLOW enum values map to the Vista MSGFLT_ADD/REMOVE
                ret = _ChangeWindowMessageFilter(message, action);
                if (!ret)
                {
                    HRESULT.ThrowLastError();
                }
                return MSGFLTINFO.NONE;
            }

            var filterstruct = new CHANGEFILTERSTRUCT { cbSize = (uint)Marshal.SizeOf(typeof(CHANGEFILTERSTRUCT)) };
            ret = _ChangeWindowMessageFilterEx(hwnd, message, action, ref filterstruct);
            if (!ret)
            {
                HRESULT.ThrowLastError();
            }

            return filterstruct.ExtStatus;
        }

        [DllImport("shell32.dll", EntryPoint = "CommandLineToArgvW", CharSet = CharSet.Unicode)]
        private static extern IntPtr _CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string cmdLine, out int numArgs);

        public static string[] CommandLineToArgvW(string cmdLine)
        {
            IntPtr argv = IntPtr.Zero;
            try
            {
                int numArgs = 0;

                argv = _CommandLineToArgvW(cmdLine, out numArgs);
                if (argv == IntPtr.Zero)
                {
                    throw new Win32Exception();
                }
                var result = new string[numArgs];

                for (int i = 0; i < numArgs; i++)
                {
                    IntPtr currArg = Marshal.ReadIntPtr(argv, i * Marshal.SizeOf(typeof(IntPtr)));
                    result[i] = Marshal.PtrToStringUni(currArg);
                }

                return result;
            }
            finally
            {

                IntPtr p = _LocalFree(argv);
                // Otherwise LocalFree failed.
                Assert.AreEqual(IntPtr.Zero, p);
            }
        }

        [DllImport("gdi32.dll", EntryPoint="CreateDIBSection", SetLastError=true)]
        private static extern SafeHBITMAP _CreateDIBSection(SafeDC hdc, [In] ref BITMAPINFO bitmapInfo, int iUsage, [Out] out IntPtr ppvBits, IntPtr hSection, int dwOffset);

        [DllImport("gdi32.dll", EntryPoint = "CreateDIBSection", SetLastError = true)]
        private static extern SafeHBITMAP _CreateDIBSectionIntPtr(IntPtr hdc, [In] ref BITMAPINFO bitmapInfo, int iUsage, [Out] out IntPtr ppvBits, IntPtr hSection, int dwOffset);

        public static SafeHBITMAP CreateDIBSection(SafeDC hdc, ref BITMAPINFO bitmapInfo, out IntPtr ppvBits, IntPtr hSection, int dwOffset)
        {
            const int DIB_RGB_COLORS = 0;
            SafeHBITMAP hBitmap = null;
            if (hdc == null)
            {
                hBitmap = _CreateDIBSectionIntPtr(IntPtr.Zero, ref bitmapInfo, DIB_RGB_COLORS, out ppvBits, hSection, dwOffset);
            }
            else
            {
                hBitmap = _CreateDIBSection(hdc, ref bitmapInfo, DIB_RGB_COLORS, out ppvBits, hSection, dwOffset);
            }

            if (hBitmap.IsInvalid)
            {
                HRESULT.ThrowLastError();
            }

            return hBitmap;
        }


        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn", SetLastError = true)]
        private static extern IntPtr _CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        public static IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse)
        {
            IntPtr ret = _CreateRoundRectRgn(nLeftRect, nTopRect, nRightRect, nBottomRect, nWidthEllipse, nHeightEllipse);
            if (IntPtr.Zero == ret)
            {
                throw new Win32Exception();
            }
            return ret;
        }

        [DllImport("gdi32.dll", EntryPoint = "CreateRectRgn", SetLastError = true)]
        private static extern IntPtr _CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

        public static IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect)
        {
            IntPtr ret = _CreateRectRgn(nLeftRect, nTopRect, nRightRect, nBottomRect);
            if (IntPtr.Zero == ret)
            {
                throw new Win32Exception();
            }
            return ret;
        }

        [DllImport("gdi32.dll", EntryPoint = "CreateRectRgnIndirect", SetLastError = true)]
        private static extern IntPtr _CreateRectRgnIndirect([In] ref RECT lprc);

        public static IntPtr CreateRectRgnIndirect(RECT lprc)
        {
            IntPtr ret = _CreateRectRgnIndirect(ref lprc);
            if (IntPtr.Zero == ret)
            {
                throw new Win32Exception();
            }
            return ret;
        }

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateSolidBrush(int crColor);

        [DllImport("user32.dll", SetLastError=true, CharSet=CharSet.Unicode, EntryPoint="CreateWindowExW")]
        private static extern IntPtr _CreateWindowEx(
            WS_EX dwExStyle,
            [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
            [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
            WS dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        public static IntPtr CreateWindowEx(
            WS_EX dwExStyle,
            string lpClassName,
            string lpWindowName,
            WS dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam)
        {
            IntPtr ret = _CreateWindowEx(dwExStyle, lpClassName, lpWindowName, dwStyle, x, y, nWidth, nHeight, hWndParent, hMenu, hInstance, lpParam);
            if (IntPtr.Zero == ret)
            {
                HRESULT.ThrowLastError();
            }

            return ret;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "DefWindowProcW")]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, WM Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr handle);

        [DllImport("user32.dll", SetLastError=true)]
        public static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hwnd);

        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

        [DllImport("dwmapi.dll", EntryPoint = "DwmIsCompositionEnabled", PreserveSig = false)]
        private static extern void _DwmIsCompositionEnabled([Out, MarshalAs(UnmanagedType.Bool)] out bool pfEnabled);

        public static bool DwmIsCompositionEnabled()
        {
            bool composited;
            _DwmIsCompositionEnabled(out composited);
            return composited;
        }

        [DllImport("dwmapi.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DwmDefWindowProc(IntPtr hwnd, WM msg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);

        [DllImport("dwmapi.dll", EntryPoint="DwmSetWindowAttribute")]
        private static extern void _DwmSetWindowAttribute(IntPtr hwnd, DWMWA dwAttribute, ref int pvAttribute, int cbAttribute);

        public static void DwmSetWindowAttributeFlip3DPolicy(IntPtr hwnd, DWMFLIP3D flip3dPolicy)
        {
            Assert.IsTrue(Utility.IsOSVistaOrNewer);
            var dwPolicy = (int)flip3dPolicy;
            _DwmSetWindowAttribute(hwnd, DWMWA.FLIP3D_POLICY, ref dwPolicy, sizeof(int)); 
        }

        public static void DwmSetWindowAttributeDisallowPeek(IntPtr hwnd, bool disallowPeek)
        {
            Assert.IsTrue(Utility.IsOSWindows7OrNewer);
            int dwDisallow = (int)(disallowPeek ? Win32Value.TRUE : Win32Value.FALSE);
            _DwmSetWindowAttribute(hwnd, DWMWA.DISALLOW_PEEK, ref dwDisallow, sizeof(int));
        }

        [DllImport("user32.dll", EntryPoint = "EnableMenuItem")]
        private static extern int _EnableMenuItem(IntPtr hMenu, SC uIDEnableItem, MF uEnable);

        public static MF EnableMenuItem(IntPtr hMenu, SC uIDEnableItem, MF uEnable)
        {
            // Returns the previous state of the menu item, or -1 if the menu item does not exist.
            int iRet = _EnableMenuItem(hMenu, uIDEnableItem, uEnable);
            return (MF)iRet;
        }

        [DllImport("user32.dll", EntryPoint = "RemoveMenu", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _RemoveMenu(IntPtr hMenu, uint uPosition, uint uFlags);

        public static void RemoveMenu(IntPtr hMenu, SC uPosition, MF uFlags)
        {
            if (!_RemoveMenu(hMenu, (uint)uPosition, (uint)uFlags))
            {
                throw new Win32Exception();
            }
        }

        [DllImport("user32.dll", EntryPoint = "DrawMenuBar", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _DrawMenuBar(IntPtr hWnd);

        public static void DrawMenuBar(IntPtr hWnd)
        {
            if (!_DrawMenuBar(hWnd))
            {
                throw new Win32Exception();
            }
        }

        [DllImport("kernel32.dll")]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FindClose(IntPtr handle);

        // Not shimming this SetLastError=true function because callers want to evaluate the reason for failure.
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFindHandle FindFirstFileW(string lpFileName, [In, Out, MarshalAs(UnmanagedType.LPStruct)] WIN32_FIND_DATAW lpFindFileData);

        // Not shimming this SetLastError=true function because callers want to evaluate the reason for failure.
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FindNextFileW(SafeFindHandle hndFindFile, [In, Out, MarshalAs(UnmanagedType.LPStruct)] WIN32_FIND_DATAW lpFindFileData);

        [DllImport("user32.dll", EntryPoint = "GetClientRect", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _GetClientRect(IntPtr hwnd, [Out] out RECT lpRect);

        public static RECT GetClientRect(IntPtr hwnd)
        {
            RECT rc;
            if (!_GetClientRect(hwnd, out rc))
            {
                HRESULT.ThrowLastError();
            }
            return rc;
        }

        [Obsolete("Use SafeDC.GetDC instead.", true)]
        public static void GetDC() { }

        [DllImport("gdi32.dll")]
        public static extern int GetDeviceCaps(SafeDC hdc, DeviceCap nIndex);

        [DllImport("user32.dll", EntryPoint = "GetMonitorInfo", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _GetMonitorInfo(IntPtr hMonitor, [In, Out] MONITORINFO lpmi);

        public static MONITORINFO GetMonitorInfo(IntPtr hMonitor)
        {
            var mi = new MONITORINFO();
            if (!_GetMonitorInfo(hMonitor, mi))
            {
                throw new Win32Exception();
            }
            return mi;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetSystemMenu(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bRevert);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(SM nIndex);

        // This is aliased as a macro in 32bit Windows.
        public static IntPtr GetWindowLongPtr(IntPtr hwnd, GWL nIndex)
        {
            IntPtr ret = IntPtr.Zero;
            if (8 == IntPtr.Size)
            {
                ret = GetWindowLongPtr64(hwnd, nIndex);
            }
            else
            {
                ret = GetWindowLongPtr32(hwnd, nIndex);
            }
            if (IntPtr.Zero == ret)
            {
                throw new Win32Exception();
            }
            return ret;
        }

        /// <summary>
        /// Sets attributes to control how visual styles are applied to a specified window.
        /// </summary>
        /// <param name="hwnd">
        /// Handle to a window to apply changes to.
        /// </param>
        /// <param name="eAttribute">
        /// Value of type WINDOWTHEMEATTRIBUTETYPE that specifies the type of attribute to set.
        /// The value of this parameter determines the type of data that should be passed in the pvAttribute parameter.
        /// Can be the following value:
        /// <list>WTA_NONCLIENT (Specifies non-client related attributes).</list>
        /// pvAttribute must be a pointer of type WTA_OPTIONS.
        /// </param>
        /// <param name="pvAttribute">
        /// A pointer that specifies attributes to set. Type is determined by the value of the eAttribute value.
        /// </param>
        /// <param name="cbAttribute">
        /// Specifies the size, in bytes, of the data pointed to by pvAttribute.
        /// </param>
        [DllImport("uxtheme.dll", PreserveSig = false)]
        public static extern void SetWindowThemeAttribute([In] IntPtr hwnd, [In] WINDOWTHEMEATTRIBUTETYPE eAttribute, [In] ref WTA_OPTIONS pvAttribute, [In] uint cbAttribute);

        [
            SuppressMessage(
                "Microsoft.Interoperability",
                "CA1400:PInvokeEntryPointsShouldExist"),
            SuppressMessage(
                "Microsoft.Portability",
                "CA1901:PInvokeDeclarationsShouldBePortable",
                MessageId = "return"),
            DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)
        ]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, GWL nIndex);

        [
            SuppressMessage(
                "Microsoft.Portability",
                "CA1901:PInvokeDeclarationsShouldBePortable",
                MessageId = "return"),
            SuppressMessage(
                "Microsoft.Interoperability",
                "CA1400:PInvokeEntryPointsShouldExist"),
            DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)
        ]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, GWL nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowPlacement(IntPtr hwnd, WINDOWPLACEMENT lpwndpl);

        public static WINDOWPLACEMENT GetWindowPlacement(IntPtr hwnd)
        {
            WINDOWPLACEMENT wndpl = new WINDOWPLACEMENT();
            if (GetWindowPlacement(hwnd, wndpl))
            {
                return wndpl;
            }
            throw new Win32Exception();
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("gdiplus.dll")]
        public static extern Status GdipCreateBitmapFromStream(IStream stream, out IntPtr bitmap);

        [DllImport("gdiplus.dll")]
        public static extern Status GdipCreateHBITMAPFromBitmap(IntPtr bitmap, out IntPtr hbmReturn, Int32 background);

        [DllImport("gdiplus.dll")]
        public static extern Status GdipCreateHICONFromBitmap(IntPtr bitmap, out IntPtr hbmReturn);

        [DllImport("gdiplus.dll")]
        public static extern Status GdipDisposeImage(IntPtr image);

        [DllImport("gdiplus.dll")]
        public static extern Status GdipImageForceValidation(IntPtr image);

        [DllImport("gdiplus.dll")]
        public static extern Status GdiplusStartup(out IntPtr token, StartupInput input, out StartupOutput output);

        [DllImport("gdiplus.dll")]
        public static extern Status GdiplusShutdown(IntPtr token);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("kernel32.dll", EntryPoint = "LocalFree", SetLastError = true)]
        private static extern IntPtr _LocalFree(IntPtr hMem);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", EntryPoint = "PostMessage", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _PostMessage(IntPtr hWnd, WM Msg, IntPtr wParam, IntPtr lParam);

        public static void PostMessage(IntPtr hWnd, WM Msg, IntPtr wParam, IntPtr lParam)
        {
            if (!_PostMessage(hWnd, Msg, wParam, lParam))
            {
                throw new Win32Exception();
            }
        }

        [DllImport("user32.dll", SetLastError=true, EntryPoint="RegisterClassExW")]
        private static extern short _RegisterClassEx([In] ref WNDCLASSEX lpwcx);

        // Note that this will throw HRESULT_FROM_WIN32(ERROR_CLASS_ALREADY_EXISTS) on duplicate registration.
        // If needed, consider adding a Try* version of this function that returns the error code since that
        // may be ignorable.
        public static short RegisterClassEx(ref WNDCLASSEX lpwcx)
        {
            short ret = _RegisterClassEx(ref lpwcx);
            if (ret == 0)
            {
                HRESULT.ThrowLastError();
            }

            return ret;
        }

        [DllImport("user32.dll", EntryPoint = "RegisterWindowMessage", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint _RegisterWindowMessage([MarshalAs(UnmanagedType.LPWStr)] string lpString);

        public static WM RegisterWindowMessage(string lpString)
        {
            uint iRet = _RegisterWindowMessage(lpString);
            if (iRet == 0)
            {
                HRESULT.ThrowLastError();
            }
            return (WM)iRet;
        }

        [DllImport("user32.dll", EntryPoint="SetActiveWindow", SetLastError=true)]
        private static extern IntPtr _SetActiveWindow(IntPtr hWnd);

        public static IntPtr SetActiveWindow(IntPtr hwnd)
        {
            Verify.IsNotDefault(hwnd, "hwnd");
            IntPtr ret = _SetActiveWindow(hwnd);
            if (ret == IntPtr.Zero)
            {
                HRESULT.ThrowLastError();
            }

            return ret;
        }

        // This is aliased as a macro in 32bit Windows.
        public static IntPtr SetClassLongPtr(IntPtr hwnd, GCLP nIndex, IntPtr dwNewLong)
        {
            if (8 == IntPtr.Size)
            {
                return SetClassLongPtr64(hwnd, nIndex, dwNewLong);
            }
            return SetClassLongPtr32(hwnd, nIndex, dwNewLong);
        }

        [DllImport("user32.dll", EntryPoint = "SetClassLong", SetLastError = true)]
        private static extern IntPtr SetClassLongPtr32(IntPtr hWnd, GCLP nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetClassLongPtr", SetLastError = true)]
        private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, GCLP nIndex, IntPtr dwNewLong);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern ErrorModes SetErrorMode(ErrorModes newMode);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "SetProcessWorkingSetSize")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimiumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        public static void SetProcessWorkingSetSize(IntPtr hProcess, int dwMinimumWorkingSetSize, int dwMaximumWorkingSetSize)
        {
            if (!_SetProcessWorkingSetSize(hProcess, new IntPtr(dwMinimumWorkingSetSize), new IntPtr(dwMaximumWorkingSetSize)))
            {
                throw new Win32Exception();
            }
        }

        // This is aliased as a macro in 32bit Windows.
        public static IntPtr SetWindowLongPtr(IntPtr hwnd, GWL nIndex, IntPtr dwNewLong)
        {
            if (8 == IntPtr.Size)
            {
                return SetWindowLongPtr64(hwnd, nIndex, dwNewLong);
            }
            return SetWindowLongPtr32(hwnd, nIndex, dwNewLong);
        }

        [
            SuppressMessage(
                "Microsoft.Portability",
                "CA1901:PInvokeDeclarationsShouldBePortable",
                MessageId = "2"),
            SuppressMessage(
                "Microsoft.Interoperability",
                "CA1400:PInvokeEntryPointsShouldExist"),
            SuppressMessage(
                "Microsoft.Portability",
                "CA1901:PInvokeDeclarationsShouldBePortable",
                MessageId = "return"),
            DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)
        ]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, GWL nIndex, IntPtr dwNewLong);

        [
            SuppressMessage(
                "Microsoft.Portability",
                "CA1901:PInvokeDeclarationsShouldBePortable",
                MessageId = "return"),
            SuppressMessage(
                "Microsoft.Interoperability",
                "CA1400:PInvokeEntryPointsShouldExist"),
            DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)
        ]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, GWL nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowRgn", SetLastError = true)]
        private static extern int _SetWindowRgn(IntPtr hWnd, IntPtr hRgn, [MarshalAs(UnmanagedType.Bool)] bool bRedraw);

        public static void SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw)
        {
            int err = _SetWindowRgn(hWnd, hRgn, bRedraw);
            if (0 == err)
            {
                throw new Win32Exception();
            }
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, SWP uFlags);

        public static void SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, SWP uFlags)
        {
            if (!_SetWindowPos(hWnd, hWndInsertAfter, x, y, cx, cy, uFlags))
            {
                throw new Win32Exception();
            }
        }

        [DllImport("shell32.dll", SetLastError = false)]
        public static extern Win32Error SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfo", SetLastError = true)]
        private static extern bool _SystemParametersInfo(SPI uiAction, int uiParam, string pvParam, SPIF fWinIni);

        public static void SystemParametersInfo(SPI uiAction, int uiParam, string pvParam, SPIF fWinIni)
        {
            if (!_SystemParametersInfo(uiAction, uiParam, pvParam, fWinIni))
            {
                throw new Win32Exception();
            }
        }

        // This function is strange in that it returns a BOOL if TPM_RETURNCMD isn't specified, but otherwise the command Id.
        // Currently it's only used with TPM_RETURNCMD, so making the signature match that.
        [DllImport("user32.dll")]
        public static extern uint TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        [DllImport("gdi32.dll", EntryPoint="SelectObject", SetLastError = true)]
        private static extern IntPtr _SelectObject(SafeDC hdc, IntPtr hgdiobj);

        public static IntPtr SelectObject(SafeDC hdc, IntPtr hgdiobj)
        {
            IntPtr ret = _SelectObject(hdc, hgdiobj);
            if (ret == IntPtr.Zero)
            {
                HRESULT.ThrowLastError();
            }
            return ret;
        }

        [DllImport("gdi32.dll", EntryPoint = "SelectObject", SetLastError = true)]
        private static extern IntPtr _SelectObjectSafeHBITMAP(SafeDC hdc, SafeHBITMAP hgdiobj);

        public static IntPtr SelectObject(SafeDC hdc, SafeHBITMAP hgdiobj)
        {
            IntPtr ret = _SelectObjectSafeHBITMAP(hdc, hgdiobj);
            if (ret == IntPtr.Zero)
            {
                HRESULT.ThrowLastError();
            }
            return ret;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SendInput(int nInputs, ref INPUT pInputs, int cbSize);

        [DllImport("user32.dll", EntryPoint="UnregisterClass", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _UnregisterClassAtom(IntPtr lpClassName, IntPtr hInstance);

        [DllImport("user32.dll", EntryPoint = "UnregisterClass", CharSet=CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _UnregisterClassName(string lpClassName, IntPtr hInstance);

        public static void UnregisterClass(short atom, IntPtr hinstance)
        {
            if (!_UnregisterClassAtom(new IntPtr(atom), hinstance))
            {
                HRESULT.ThrowLastError();
            }
        }

        [DllImport("user32.dll", SetLastError=true, EntryPoint="UpdateLayeredWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _UpdateLayeredWindow(
            IntPtr hwnd, 
            SafeDC hdcDst, 
            [In] ref POINT pptDst, 
            [In] ref SIZE psize, 
            SafeDC hdcSrc, 
            [In] ref POINT pptSrc,
            int crKey,
            ref BLENDFUNCTION pblend,
            ULW dwFlags);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "UpdateLayeredWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _UpdateLayeredWindowIntPtr(
            IntPtr hwnd,
            IntPtr hdcDst,
            IntPtr pptDst,
            IntPtr psize,
            IntPtr hdcSrc,
            IntPtr pptSrc,
            int crKey,
            ref BLENDFUNCTION pblend,
            ULW dwFlags);

        public static void UpdateLayeredWindow(
            IntPtr hwnd,
            SafeDC hdcDst,
            ref POINT pptDst,
            ref SIZE psize,
            SafeDC hdcSrc,
            ref POINT pptSrc,
            int crKey,
            ref BLENDFUNCTION pblend,
            ULW dwFlags)
        {
            if (!_UpdateLayeredWindow(hwnd, hdcDst, ref pptDst, ref psize, hdcSrc, ref pptSrc, crKey, ref pblend, dwFlags))
            {
                HRESULT.ThrowLastError();
            }
        }

        public static void UpdateLayeredWindow(
            IntPtr hwnd,
            int crKey,
            ref BLENDFUNCTION pblend,
            ULW dwFlags)
        {
            if (!_UpdateLayeredWindowIntPtr(hwnd, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, crKey, ref pblend, dwFlags))
            {
                HRESULT.ThrowLastError();
            }
        }

        #region Win7 declarations

        //#define DWM_SIT_DISPLAYFRAME    0x00000001  // Display a window frame around the provided bitmap

        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmInvalidateIconicBitmaps(IntPtr hwnd);

        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmSetIconicThumbnail(IntPtr hwnd, IntPtr hbmp, DWM_SIT dwSITFlags);

        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmSetIconicLivePreviewBitmap(IntPtr hwnd, IntPtr hbmp, RefPOINT pptClient, DWM_SIT dwSITFlags);

        [DllImport("shell32.dll", PreserveSig = false)]
        public static extern void SHGetItemFromDataObject(IDataObject pdtobj, DOGIF dwFlags, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out object ppv);

        [DllImport("shell32.dll", PreserveSig = false, EntryPoint = "SHAddToRecentDocs")]
        private static extern void _SHAddToRecentDocsObj(SHARD uFlags, object pv);

        [DllImport("shell32.dll", EntryPoint = "SHAddToRecentDocs")]
        private static extern void _SHAddToRecentDocs_String(SHARD uFlags, [MarshalAs(UnmanagedType.LPWStr)] string pv);

        // This overload is required.  There's a cast in the Shell code that causes the wrong vtbl to be used
        // if we let the marshaller convert the parameter to an IUnknown.
        [DllImport("shell32.dll", EntryPoint = "SHAddToRecentDocs")]
        private static extern void _SHAddToRecentDocs_ShellLink(SHARD uFlags, IShellLinkW pv);

        public static void SHAddToRecentDocs(string path)
        {
            _SHAddToRecentDocs_String(SHARD.PATHW, path);
        }

        // Win7 only.
        public static void SHAddToRecentDocs(IShellLinkW shellLink)
        {
            _SHAddToRecentDocs_ShellLink(SHARD.LINK, shellLink);
        }

        public static void SHAddToRecentDocs(SHARDAPPIDINFO info)
        {
            _SHAddToRecentDocsObj(SHARD.APPIDINFO, info);
        }

        public static void SHAddToRecentDocs(SHARDAPPIDINFOIDLIST infodIdList)
        {
            _SHAddToRecentDocsObj(SHARD.APPIDINFOIDLIST, infodIdList);
        }

        [DllImport("shell32.dll", PreserveSig = false)]
        public static extern HRESULT SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IBindCtx pbc, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out object ppv);

        [DllImport("shell32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Shell_NotifyIcon(NIM dwMessage, [In] NOTIFYICONDATA lpdata);

        /// <summary>
        /// Sets the User Model AppID for the current process, enabling Windows to retrieve this ID
        /// </summary>
        /// <param name="AppID"></param>
        [DllImport("shell32.dll", PreserveSig = false)]
        public static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        /// <summary>
        /// Retrieves the User Model AppID that has been explicitly set for the current process via SetCurrentProcessExplicitAppUserModelID
        /// </summary>
        /// <param name="AppID"></param>
        [DllImport("shell32.dll")]
        public static extern HRESULT GetCurrentProcessExplicitAppUserModelID([Out, MarshalAs(UnmanagedType.LPWStr)] out string AppID);

        #endregion
    }
}
