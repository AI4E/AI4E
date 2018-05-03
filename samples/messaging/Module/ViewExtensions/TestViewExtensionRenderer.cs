using AI4E;
using AI4E.Modularity;
using Shared.ViewExtensions;

namespace Host.ViewExtensions
{
    public class TestViewExtensionRenderer : ViewExtensionRenderer
    {
        public IDispatchResult Handle(TestViewExtension viewExtension)
        {
            return View("ViewExtensions/TestViewExtension", viewExtension);
        }
    }
}
