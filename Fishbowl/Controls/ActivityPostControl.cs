using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Contigo;
using System.Windows.Controls;

namespace FacebookClient
{
    public class ActivityPostControl : Control
    {
        public static readonly DependencyProperty ActivityPostProperty = DependencyProperty.Register(
            "ActivityPost",
            typeof(ActivityPost),
            typeof(ActivityPostControl),
            new FrameworkPropertyMetadata(null));

        public ActivityPost ActivityPost
        {
            get { return (ActivityPost)GetValue(ActivityPostProperty); }
            set { SetValue(ActivityPostProperty, value); }
        }
    }
}
