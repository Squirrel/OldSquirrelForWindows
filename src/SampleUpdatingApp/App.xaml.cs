using Squirrel.Client;

namespace SampleUpdatingApp
{
    public partial class App
    {
    }

    public class SimpleUpdatingAppSetup : AppSetup
    {
        public override string ShortcutName {
            get { return "SimpleUpdatingApp"; } 
        }
    }
}
