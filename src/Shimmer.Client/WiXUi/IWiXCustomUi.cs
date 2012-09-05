using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TinyIoC;

namespace Shimmer.Client.WiXUi
{
    public interface IWiXCustomUi
    {
        void RegisterTypes(TinyIoCContainer kernel);
    }
}
