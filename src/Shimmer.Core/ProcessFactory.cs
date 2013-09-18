using System.Diagnostics;

namespace Shimmer.Core
{
    public interface IProcessFactory
    {
        void Start(string path);
    }

    public class DefaultProcessFactory : IProcessFactory
    {
        public void Start(string path)
        {
            Process.Start(path);
        }
    }
}
