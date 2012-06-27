using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using Shimmer.Client;

namespace SampleUpdatingApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
    }

    public class SimpleUpdatingAppSetup : AppSetup
    {
        public override string ShortcutName {
            get { return "SimpleUpdatingApp"; } 
        }
    }
}