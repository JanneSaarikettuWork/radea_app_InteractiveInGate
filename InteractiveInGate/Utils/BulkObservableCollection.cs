using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace InteractiveInGate.Utils
{
    /* A ObservableCollection<T> that supports performing bulk operations (so that events
     * aren't fired for each operation performed on the collection) */
    class BulkObservableCollection<T> : ObservableCollection<T>
    {
        protected bool performingBulkOperation = false;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!performingBulkOperation)
                base.OnCollectionChanged(e);
        }
        public void BeginBulkOperation()
        {
            performingBulkOperation = true;
        }
        public void EndBulkOperation()
        {
            performingBulkOperation = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
