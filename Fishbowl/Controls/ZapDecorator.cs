
namespace FacebookClient
{
    using System;
    using System.Windows.Controls;
    using System.Windows;
    using System.Windows.Media;
    using Standard;
    using System.Windows.Threading;
    using System.Diagnostics;

    public class ZapDecorator : Decorator
    {
        private class CompositionTargetRenderingListener : DispatcherObject, IDisposable
        {
            public CompositionTargetRenderingListener() { }

            public void StartListening()
            {
                requireAccessAndNotDisposed();

                if (!m_isListening)
                {
                    m_isListening = true;
                    CompositionTarget.Rendering += compositionTarget_Rendering;
                }
            }

            public void StopListening()
            {
                requireAccessAndNotDisposed();

                if (m_isListening)
                {
                    m_isListening = false;
                    CompositionTarget.Rendering -= compositionTarget_Rendering;
                }
            }

            public void WireParentLoadedUnloaded(FrameworkElement parent)
            {
                requireAccessAndNotDisposed();
                //Util.RequireNotNull(parent, "parent");

                parent.Loaded += delegate(object sender, RoutedEventArgs e)
                {
                    this.StartListening();
                };

                parent.Unloaded += delegate(object sender, RoutedEventArgs e)
                {
                    this.StopListening();
                };
            }

            public bool IsDisposed
            {
                get
                {
                    VerifyAccess();
                    return m_disposed;
                }
            }

            public event EventHandler Rendering;

            protected virtual void OnRendering(EventArgs args)
            {
                requireAccessAndNotDisposed();

                EventHandler handler = Rendering;
                if (handler != null)
                {
                    handler(this, args);
                }
            }

            public void Dispose()
            {
                requireAccessAndNotDisposed();
                StopListening();

                Delegate[] invocationlist = Rendering.GetInvocationList();
                foreach (Delegate d in invocationlist)
                {
                    Rendering -= (EventHandler)d;
                }

                m_disposed = true;
            }

            #region Implementation

            [DebuggerStepThrough]
            private void requireAccessAndNotDisposed()
            {
                VerifyAccess();
                if (m_disposed)
                {
                    throw new ObjectDisposedException(string.Empty);
                }
            }

            private void compositionTarget_Rendering(object sender, EventArgs e)
            {
                OnRendering(e);
            }

            private bool m_isListening;
            private bool m_disposed;

            #endregion

        }

        public ZapDecorator()
        {
            m_listener.Rendering += m_listener_rendering;
            m_listener.WireParentLoadedUnloaded(this);

        }

        public static readonly DependencyProperty TargetIndexProperty =
            DependencyProperty.Register("TargetIndex", typeof(int), typeof(ZapDecorator),
            new FrameworkPropertyMetadata(0, new PropertyChangedCallback(targetIndexChanged)));

        public int TargetIndex
        {
            get { return (int)GetValue(TargetIndexProperty); }
            set { SetValue(TargetIndexProperty, value); }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            UIElement child = this.Child;
            if (child != null)
            {
                m_listener.StartListening();
                child.Measure(availableSize);
            }
            return new Size();
        }

        #region Implementation

        private static bool _Animate(
            double currentValue, double currentVelocity, double targetValue,
            double attractionFator, double dampening,
            double terminalVelocity, double minValueDelta, double minVelocityDelta,
            out double newValue, out double newVelocity)
        {
            Assert.IsTrue(DoubleUtilities.IsFinite(currentValue));
            Assert.IsTrue(DoubleUtilities.IsFinite(currentVelocity));
            Assert.IsTrue(DoubleUtilities.IsFinite(targetValue));

            Assert.IsTrue(DoubleUtilities.IsFinite(dampening));
            Assert.IsTrue(dampening > 0 && dampening < 1);

            Assert.IsTrue(DoubleUtilities.IsFinite(attractionFator));
            Assert.IsTrue(attractionFator > 0);

            Assert.IsTrue(terminalVelocity > 0);

            Assert.IsTrue(minValueDelta > 0);
            Assert.IsTrue(minVelocityDelta > 0);

            double diff = targetValue - currentValue;

            if (Math.Abs(diff) > minValueDelta || Math.Abs(currentVelocity) > minVelocityDelta)
            {
                newVelocity = currentVelocity * (1 - dampening);
                newVelocity += diff * attractionFator;
                newVelocity *= (Math.Abs(currentVelocity) > terminalVelocity) ? terminalVelocity / Math.Abs(currentVelocity) : 1;

                newValue = currentValue + newVelocity;

                return true;
            }
            else
            {
                newValue = targetValue;
                newVelocity = 0;
                return false;
            }
        }

        private void m_listener_rendering(object sender, EventArgs e)
        {
            if (this.Child != m_zapPanel)
            {
                m_zapPanel = (ZapPanel)this.Child;
                m_zapPanel.RenderTransform = m_traslateTransform;
            }
            if (m_zapPanel != null)
            {
                int actualTargetIndex = Math.Max(0, Math.Min(m_zapPanel.Children.Count - 1, TargetIndex));

                double targetPercentOffset = -actualTargetIndex / (double)m_zapPanel.Children.Count;
                targetPercentOffset = double.IsNaN(targetPercentOffset) ? 0 : targetPercentOffset;

                bool stopListening = !_Animate(
                    m_percentOffset, m_velocity, targetPercentOffset,
                    .1, .4, .1, c_diff, c_diff,
                    out m_percentOffset, out m_velocity);

                double targetPixelOffset = m_percentOffset * (this.RenderSize.Width * m_zapPanel.Children.Count);
                m_traslateTransform.X = targetPixelOffset;

                if (stopListening)
                {
                    m_listener.StopListening();
                }

            }
        }

        private static void targetIndexChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            ((ZapDecorator)element).m_listener.StartListening();
        }

        private double m_velocity;
        private double m_percentOffset;

        private ZapPanel m_zapPanel;
        private readonly TranslateTransform m_traslateTransform = new TranslateTransform();
        private readonly CompositionTargetRenderingListener m_listener = new CompositionTargetRenderingListener();

        private const double c_diff = .00001;

        #endregion

    }

}