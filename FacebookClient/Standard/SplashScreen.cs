
namespace Standard
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Resources;
    using System.Runtime.InteropServices;
    using System.Windows.Threading;

    // Current issues with this implementation:
    // *  FadeOutDuration will pop the splashscreen in front of the main window.  This can be partially managed
    //        by using IsTopMost, but that has other effects.  I should be able to hook the WndProc to keep this
    //        window from going inactive.
    // * FadeInDuration doesn't work because this is being created on the main UI thread.  For multiple reasons we
    //        should probably create this window on a background thread.
    public class SplashScreen
    {
        private static readonly BLENDFUNCTION _BaseBlendFunction = new BLENDFUNCTION
        {
            BlendOp = AC.SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = AC.SRC_ALPHA,
        };

        private static WndProc _DefWindowProcDelegate = NativeMethods.DefWindowProc;

        private IntPtr _hwnd;
        private IntPtr _hInstance;
        private SafeHBITMAP _hBitmap;
        private short _classAtom;
        private DispatcherTimer _dt;
        private DateTime _fadeOutEnd;
        private DateTime _fadeInEnd;
        private ResourceManager _resourceManager;
        private string _resourceName;
        private Dispatcher _dispatcher;
        private Assembly _resourceAssembly;
        private bool _isClosed = false;

        private const string CLASSNAME = "WPF Splash Screen";

        private void _VerifyMutability()
        {
            if (_hwnd != IntPtr.Zero)
            {
                throw new InvalidOperationException("Splash screen has already been shown.");
            }
        }

        public SplashScreen() { }

        public Assembly ResourceAssembly
        { 
            get { return _resourceAssembly; }
            set
            {
                _VerifyMutability();

                Verify.IsNotNull(value, "value");

                _resourceAssembly = value;
                _hInstance = Marshal.GetHINSTANCE(_resourceAssembly.ManifestModule);
                AssemblyName name = new AssemblyName(_resourceAssembly.FullName);
                _resourceManager = new ResourceManager(name.Name + ".g", _resourceAssembly);
            }
        }

        public string ResourceName
        {
            get { return _resourceName ?? ""; }
            set
            {
                Verify.IsNeitherNullNorEmpty(value, "value");
                _resourceName = value.ToLowerInvariant();
            }
        }

        public string ImageFileName { get; set; }
        public bool IsTopMost { get; set; }
        public bool CloseOnMainWindowCreation { get; set; }
        public TimeSpan FadeOutDuration { get; set; }
        public TimeSpan FadeInDuration { get; set; }

        public void Show()
        {
            _VerifyMutability();

            Stream imageStream = null;
            try
            {
                // Try to use the filepath first.  If it's not provided or not available, use the embedded resource.
                if (!string.IsNullOrEmpty(ImageFileName) && File.Exists(ImageFileName))
                {
                    try
                    {
                        imageStream = new FileStream(ImageFileName, FileMode.Open);
                    }
                    catch (IOException) { }
                }

                if (imageStream == null)
                {
                    imageStream = _resourceManager.GetStream(ResourceName, CultureInfo.CurrentUICulture);
                    if (imageStream == null)
                    {
                        throw new IOException("The resource could not be found.");
                    }
                }

                SIZE bitmapSize;
                _hBitmap = _CreateHBITMAPFromImageStream(imageStream, out bitmapSize);

                _hwnd = _CreateWindow(bitmapSize);

                byte opacity = (byte)(FadeInDuration > TimeSpan.Zero ? 0 : 255);
                _ApplyBitmapToLayeredWindow(_hwnd, _hBitmap, opacity);

                if (CloseOnMainWindowCreation)
                {
                    Dispatcher.CurrentDispatcher.BeginInvoke(
                        DispatcherPriority.Loaded,
                        (DispatcherOperationCallback)delegate(object splashObj)
                        {
                            var splashScreen = (SplashScreen)splashObj;
                            if (!splashScreen._isClosed)
                            {
                                splashScreen.Close();
                            }
                            return null;
                        },
                        this);
                }

                _dispatcher = Dispatcher.CurrentDispatcher;
                if (FadeInDuration > TimeSpan.Zero)
                {
                    _fadeInEnd = DateTime.UtcNow + FadeInDuration;
                    _dt = new DispatcherTimer(FadeInDuration, DispatcherPriority.Normal, _FadeInTick, _dispatcher);
                    _dt.Start();
                }
            }
            finally
            {
                Utility.SafeDispose(ref imageStream);
            }
        }

        private IntPtr _CreateWindow(SIZE size)
        {
            var wndClass = new WNDCLASSEX
            {
                cbSize = Marshal.SizeOf(typeof(WNDCLASSEX)),
                style = CS.HREDRAW | CS.VREDRAW,
                hInstance = _hInstance,
                hCursor = IntPtr.Zero,
                lpszClassName = CLASSNAME,
                lpszMenuName = string.Empty,
                lpfnWndProc = _DefWindowProcDelegate,
            };

            _classAtom = NativeMethods.RegisterClassEx(ref wndClass);

            int screenWidth = NativeMethods.GetSystemMetrics(SM.CXSCREEN);
            int screenHeight = NativeMethods.GetSystemMetrics(SM.CYSCREEN);
            int x = (screenWidth - size.cx) / 2;
            int y = (screenHeight - size.cy) / 2;

            WS_EX windowCreateFlags = WS_EX.WINDOWEDGE | WS_EX.TOOLWINDOW | WS_EX.LAYERED | (IsTopMost ? WS_EX.TOPMOST : 0);

            IntPtr hWnd = NativeMethods.CreateWindowEx(
                windowCreateFlags,
                CLASSNAME, "Splash Screen",
                WS.POPUP | WS.VISIBLE,
                x, y,
                size.cx, size.cy,
                IntPtr.Zero, 
                IntPtr.Zero, 
                _hInstance, IntPtr.Zero);

            return hWnd;
        }

        public void Close()
        {
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.Invoke(DispatcherPriority.Normal, (Action)Close);
                return;
            }

            if (_isClosed)
            {
                throw new InvalidOperationException("Splash screen was already closed");
            }

            _isClosed = true;

            if (FadeOutDuration <= TimeSpan.Zero)
            {
                _DestroyResources();
                return;
            }

            try
            {
                NativeMethods.SetActiveWindow(_hwnd);
            }
            catch
            {
                // SetActiveWindow fails if the application is not in the foreground.
                // If this is the case, don't bother animating the fade out.
                _DestroyResources();
                return;
            }

            _fadeOutEnd = DateTime.UtcNow + FadeOutDuration;
            if (_dt != null)
            {
                _dt.Stop();
            }
            _dt = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Normal, _FadeOutTick, _dispatcher);
            _dt.Start();

            return;
        }

        private void _FadeOutTick(object unused, EventArgs args)
        {
            DateTime dtNow = DateTime.UtcNow;
            if (dtNow >= _fadeOutEnd)
            {
                _DestroyResources();
            }
            else
            {
                double progress = (_fadeOutEnd - dtNow).TotalMilliseconds / FadeOutDuration.TotalMilliseconds;
                BLENDFUNCTION bf = _BaseBlendFunction;
                bf.SourceConstantAlpha = (byte)(255 * progress);
                NativeMethods.UpdateLayeredWindow(_hwnd, 0, ref bf, ULW.ALPHA);
            }
        }

        private void _FadeInTick(object unused, EventArgs args)
        {
            DateTime dtNow = DateTime.UtcNow;
            if (dtNow >= _fadeInEnd)
            {
                _DestroyResources();
            }
            else
            {
                double progress = 1 - (_fadeInEnd - dtNow).TotalMilliseconds / FadeInDuration.TotalMilliseconds;
                progress = Math.Max(0, Math.Min(progress, 1));
                BLENDFUNCTION bf = _BaseBlendFunction;
                bf.SourceConstantAlpha = (byte)(int)(255 * progress);
                NativeMethods.UpdateLayeredWindow(_hwnd, 0, ref bf, ULW.ALPHA);
            }
        }

        private void _DestroyResources()
        {
            if (_dt != null)
            {
                _dt.Stop();
                _dt = null;
            }
            Utility.SafeDestroyWindow(ref _hwnd);
            Utility.SafeDispose(ref _hBitmap);
            // Currently not releasing the class atom.
            //if (_classAtom != 0)
            //{
            //    NativeMethods.UnregisterClass(_classAtom, _hInstance);
            //    _classAtom = 0;
            //}
            if (_resourceManager != null)
            {
                _resourceManager.ReleaseAllResources();
            }
        }

        private static SafeHBITMAP _CreateHBITMAPFromImageStream(Stream imgStream, out SIZE bitmapSize)
        {
            IWICImagingFactory pImagingFactory = null;
            IWICBitmapDecoder pDecoder = null;
            IWICStream pStream = null;
            IWICBitmapFrameDecode pDecodedFrame = null;
            IWICFormatConverter pBitmapSourceFormatConverter = null;
            IWICBitmapFlipRotator pBitmapFlipRotator = null;

            SafeHBITMAP hbmp = null;
            try
            {
                using (var istm = new ManagedIStream(imgStream))
                {
                    pImagingFactory = CLSID.CoCreateInstance<IWICImagingFactory>(CLSID.WICImagingFactory);
                    pStream = pImagingFactory.CreateStream();
                    pStream.InitializeFromIStream(istm);

                    // Create an object that will decode the encoded image
                    Guid vendor = Guid.Empty;
                    pDecoder = pImagingFactory.CreateDecoderFromStream(pStream, ref vendor, WICDecodeMetadata.CacheOnDemand);

                    pDecodedFrame = pDecoder.GetFrame(0);
                    pBitmapSourceFormatConverter = pImagingFactory.CreateFormatConverter();

                    // Convert the image from whatever format it is in to 32bpp premultiplied alpha BGRA
                    Guid pixelFormat = WICPixelFormat.WICPixelFormat32bppPBGRA;
                    pBitmapSourceFormatConverter.Initialize(pDecodedFrame, ref pixelFormat, WICBitmapDitherType.None, IntPtr.Zero, 0, WICBitmapPaletteType.Custom);

                    pBitmapFlipRotator = pImagingFactory.CreateBitmapFlipRotator();
                    pBitmapFlipRotator.Initialize(pBitmapSourceFormatConverter, WICBitmapTransform.FlipVertical);

                    int width, height;
                    pBitmapFlipRotator.GetSize(out width, out height);

                    bitmapSize = new SIZE { cx = width, cy = height };

                    var bmi = new BITMAPINFO
                    {
                        bmiHeader = new BITMAPINFOHEADER
                        {
                            biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                            biWidth = width,
                            biHeight = height,
                            biPlanes = 1,
                            biBitCount = 32,
                            biCompression = BI.RGB,
                            biSizeImage = (width * height * 4),
                        },
                    };

                    // Create a 32bpp DIB.  This DIB must have an alpha channel for UpdateLayeredWindow to succeed.
                    IntPtr pBitmapBits;
                    hbmp = NativeMethods.CreateDIBSection(null, ref bmi, out pBitmapBits, IntPtr.Zero, 0);

                    // Copy the decoded image to the new buffer which backs the HBITMAP
                    var rect = new WICRect { X = 0, Y = 0, Width = width, Height = height };
                    pBitmapFlipRotator.CopyPixels(ref rect, width * 4, bmi.bmiHeader.biSizeImage, pBitmapBits);

                    var ret = hbmp;
                    hbmp = null;
                    return ret;
                }
            }
            finally
            {
                Utility.SafeRelease(ref pImagingFactory);
                Utility.SafeRelease(ref pDecoder);
                Utility.SafeRelease(ref pStream);
                Utility.SafeRelease(ref pDecodedFrame);
                Utility.SafeRelease(ref pBitmapFlipRotator);
                Utility.SafeRelease(ref pBitmapSourceFormatConverter);
                Utility.SafeDispose(ref hbmp);
            }
        }

        private static void _ApplyBitmapToLayeredWindow(IntPtr hwnd, SafeHBITMAP bitmap, byte opacity)
        {
            using (SafeDC hScreenDC = SafeDC.GetDesktop())
            {
                using (SafeDC memDC = SafeDC.CreateCompatibleDC(hScreenDC))
                {
                    IntPtr hOldBitmap = NativeMethods.SelectObject(memDC, bitmap);

                    RECT hwndRect;
                    NativeMethods.GetWindowRect(hwnd, out hwndRect);
                    
                    POINT hwndPos = hwndRect.Position;
                    SIZE hwndSize = hwndRect.Size;
                    POINT origin = new POINT();
                    BLENDFUNCTION bf = _BaseBlendFunction;
                    bf.SourceConstantAlpha = opacity;

                    NativeMethods.UpdateLayeredWindow(hwnd, hScreenDC, ref hwndPos, ref hwndSize, memDC, ref origin, 0, ref bf, ULW.ALPHA);
                    NativeMethods.SelectObject(memDC, hOldBitmap);
                }
            }
        }
    }
}