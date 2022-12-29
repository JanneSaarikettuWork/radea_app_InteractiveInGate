using System;
using System.Threading;
using System.Windows;

namespace InteractiveInGate
{
    public class ResourceDictionaryLocator : ResourceDictionary
    {
        public ResourceDictionaryLocator()
        {
            base.Source = new Uri($"pack://application:,,,/Languages/{Thread.CurrentThread.CurrentCulture.Name}.xaml");
        }
    }
}
