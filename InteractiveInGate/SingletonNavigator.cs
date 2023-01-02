using InteractiveInGate.Views;
using System.Windows.Controls;

namespace InteractiveInGate
{
    public class SingletonNavigator
    {
        private readonly Frame rootFrame;
        private readonly StartPage startPage;

        // TODO
        //private readonly ProgressPage progressPage;
        //private readonly ItemsPage itemsPage;
        //private readonly PingPage pingPage;

        public SingletonNavigator(Frame root)
        {
            rootFrame = root;
            startPage = new StartPage(this);
            //progressPage = new ProgressPage(this);
            //itemsPage = new ItemsPage(this);
            //pingPage = new PingPage(this);
        }

        public PageView PreviousPage = PageView.Start;
        public PageView CurrentPage = PageView.Start;

        public enum PageView {
            Start,
            Progress,
            Items,
            Ping,
        };

        public void Navigate(PageView target)
        {
            CurrentPage = target;

            switch (target)
            {
                case PageView.Start:
                    rootFrame.Content = startPage;
                    break;
            //    case PageView.Progress:
            //        rootFrame.Content = progressPage;
            //        break;
            //    case PageView.Items:
            //        rootFrame.Content = itemsPage;
            //        break;
            //    case PageView.Ping:
            //        rootFrame.Content = pingPage;
            //        break;
            }
        }
    }
}
