// #define LOCATIONS_COUNT_TEST

using InteractiveInGate.ViewModels;
using Synchronize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;


namespace InteractiveInGate.Views
{
    /// <summary>
    /// Interaction logic for StartPage.xaml
    /// </summary>
    public partial class StartPage : Page
    {
        private SingletonNavigator navigator;
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        List<string> locationHierarchyNames = new List<string>();
        private List<Models.SimpleLocation> topLevelLocations;
        private List<Models.SimpleLocation> currentlyPlacedLocations;
        private Stack<List<Models.SimpleLocation>> placedLocationsStack;

        public StartPage(SingletonNavigator nav)
        {
            navigator = nav;
            InitializeComponent();


        }

        private void ResetState()
        {
            scanInProgress = false;

            Locations.RowDefinitions.Clear();
            Locations.ColumnDefinitions.Clear();
            Locations.Children.Clear();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ResetState();

            var vm = DataContext as InteractiveInGateViewModel;

#if LOCATIONS_COUNT_TEST
            var locations = new List<LocationNode>();

            for (int lindex = 1; lindex <= 400; lindex++)
            {
                locations.Add(new LocationNode($"Hotel{lindex}", "1234", "2345", null, null));
            }

#else
            var locations = vm.process.Locations.Keys.ToList();
#endif

            topLevelLocations = new List<Models.SimpleLocation>();
            foreach (LocationNode node in locations)
            {
                topLevelLocations.Add(new Models.SimpleLocation(node));
            }

            Models.SimpleLocation.CullDirectChildren(ref topLevelLocations);

            // Print out the resolved InteractiveInGateDestination location hierarchy
            foreach (Models.SimpleLocation loc in topLevelLocations)
            {
                logger.Debug(loc.Describe());
            }

            placedLocationsStack = new Stack<List<Models.SimpleLocation>>();
            placedLocationsStack.Push(topLevelLocations);
            currentlyPlacedLocations = topLevelLocations;
            logger.Debug($"Found {topLevelLocations.Count} top level destination locations.");

            locationHierarchyNames.Clear();
            PlaceLocations(topLevelLocations);

            if (vm.process.radeaFreshedAction == null)
            {
                vm.process.radeaFreshedAction = () =>
                {
                    if (!scanInProgress)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ResetState();
                            locationHierarchyNames.Clear();
                            PlaceLocations(topLevelLocations);
                        });
                    }
                };
            }
        }

        private RadioButton checkedButton = null;
        private readonly object checkedButtonLock = new object();
        private bool blinkOn = false;

        private void AnimateCheckedButton(object sender, EventArgs e)
        {
            lock (checkedButtonLock)
            {
                if (checkedButton != null && checkedButton.Foreground != null)
                {
                    if (blinkOn)
                    {
                        checkedButton.Foreground.Opacity = 1.0;
                        checkedButton.Background.Opacity = 1.0;
                    }
                    else
                    {
                        checkedButton.Foreground.Opacity = 0.55;
                        checkedButton.Background.Opacity = 0.65;
                    }
                    blinkOn = !blinkOn;
                }
            }
        }

            private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            lock (checkedButtonLock)
            {
                var b = sender as RadioButton;
                checkedButton = b;
                b.Background.Opacity = 1.0;
                b.FontWeight = FontWeights.UltraBold;
                b.Foreground.Opacity = 1.0;
                b.BorderThickness = new Thickness(5);
            }
        }

        private void RadioButton_Unchecked(object sender, RoutedEventArgs e)
        {
            lock (checkedButtonLock)
            {
                var b = sender as RadioButton;
                if (checkedButton == b)
                {
                    checkedButton = null;
                }
                b.Background.Opacity = 0.75;
                b.FontWeight = FontWeights.UltraLight;
                b.Foreground.Opacity = 0.55;
                b.BorderThickness = new Thickness(1);
            }
        }

        private bool scanInProgress = false;
        private async void Location_Click(object sender, RoutedEventArgs e)
        {
            /* Make sure we only start one scan. Double clicking on a location will result in this method
             * being called twice; starting two RFID scans (via vm.StartAsync) and crashing because
             * NavigationService is null (probably because this Page isn't shown anymore so we don't have
             * access to NavigationService). This is probably caused by the Delay (seems to be used to give
             * visual feedback of the activated location). */
            if (scanInProgress)
                return;

            var button = (sender as RadioButton);
            
            button.Background = (Brush)Application.Current.FindResource("InteractiveInGateClickedBackround");

            var vm = (DataContext as InteractiveInGateViewModel);
            var location = button.Resources["location"] as Models.SimpleLocation; // User-selected location
            if (location != null && location.Children.Count <= 1)  // Leaf node
            {
                scanInProgress = true;
                locationHierarchyNames.Add(location.Name);
                var name = string.Join("/", locationHierarchyNames);

                LocationNode targetNode = location.GetBottomLocationNode();
                logger.Info($"Delivery location for next scan: {targetNode.Name} ({targetNode.Uuid})");

                var scanTags = vm.StartAsync(targetNode, name);
                await Task.Delay(TimeSpan.FromMilliseconds(250));
                navigator.Navigate(SingletonNavigator.PageView.Progress);
                await scanTags;
            }
            else
            {
                ResetState();

                if (location != null)
                {
                    // Go down in location hierarchy
                    logger.Debug("Going down");
                    locationHierarchyNames.Add(location.Name);
                    placedLocationsStack.Push(currentlyPlacedLocations);
                    PlaceLocations(location.Children);
                }
                else
                {
                    // Go up in location hierarchy
                    logger.Debug("Going up");

                    logger.Debug("placedLocationsStack:");
                    foreach (List<Models.SimpleLocation> locList in placedLocationsStack)
                    {
                        logger.Debug("*****");
                        foreach (Models.SimpleLocation loc in locList)
                        {
                            logger.Debug(loc.Name);
                        }
                    }

                    if (locationHierarchyNames.Count > 0)
                    {
                        locationHierarchyNames.RemoveAt(locationHierarchyNames.Count-1);
                        PlaceLocations(placedLocationsStack.Pop());
                    }
                    else
                    {
                        logger.Warn("Tried to traverse above top level locations.");

                        placedLocationsStack.Clear();
                        placedLocationsStack.Push(topLevelLocations);
                        PlaceLocations(topLevelLocations);
                    }
                }
            }
            e.Handled = true;
        }


        private void PlaceLocations(List<Models.SimpleLocation> locationsToRender)
        {
            currentlyPlacedLocations = locationsToRender;

            // Add a "Back" button if we're not at the top of the hierarchy
            List<Models.SimpleLocation> listLocations = new List<Models.SimpleLocation>();
            if (locationsToRender == topLevelLocations)
            {
                listLocations = locationsToRender;
            }
            else
            {
                // This null location will be rendered as the back button.
                listLocations.Add(null);

                foreach (Models.SimpleLocation loc in locationsToRender)
                {
                    listLocations.Add(loc);
                }
            }

            // IF locations sorting = alphabetical by name
            // if (true) // TODO

            // Sort or not - for comparing the results
            Random rand = new Random();
            if (rand.NextDouble() >= 0.5) // TODO
            {


                List<Models.SimpleLocation> sortedLocations = new List<Models.SimpleLocation>();
                List<Models.SimpleLocation> sortedNonLeafLocations = new List<Models.SimpleLocation>();
                List<Models.SimpleLocation> sortedLeafLocations = new List<Models.SimpleLocation>();

                // Add link to upper level in the location tree, if there is a upper level
                if (listLocations.Contains(null)) 
                {
                    sortedLocations.Add(null);
                }

                // Sort and add non null and non leaf locations
                foreach (Models.SimpleLocation loc in listLocations) 
                { 
                    if (loc == null)
                        continue;

                    // In UI node seems to be handled as non-leaf only if it has two of more children
                    // if (loc.IsLeafNode == false)
                    if (loc.Children.Count >= 2)
                        sortedNonLeafLocations.Add(loc);
                }

                sortedNonLeafLocations.Sort();
                sortedLocations.AddRange(sortedNonLeafLocations);

                // Sort and add non null and leaf locations
                foreach (Models.SimpleLocation loc in listLocations)
                {
                    if (loc == null)
                        continue;

                    // In UI node seems to be handled as leaf if it has one child or less
                    // if (loc.IsLeafNode)
                    if (loc.Children.Count <= 1)
                        sortedLeafLocations.Add(loc);
                }

                sortedLeafLocations.Sort();
                sortedLocations.AddRange(sortedLeafLocations);

                listLocations = sortedLocations;
            }



            int RowsCount = listLocations.Count;

            if (RowsCount >= 4)
                RowsCount = Convert.ToInt32(Math.Ceiling(Math.Sqrt(RowsCount))); // Try to make the layout close to rectangular excepth the case when less than 4 items to be added
            else if (RowsCount <= 2)
                RowsCount = 1; // JSa TTR: make two buttons appear side-by-side (..rather than on vertically stacked)
            else // 3
                RowsCount = 2;


            for (int i = 0; i < RowsCount; i++)
                Locations.RowDefinitions.Add(new RowDefinition());

            for (int i = 0; i < listLocations.Count; i++)
            // for (int i = listLocations.Count - 1; i >= 0; i--)
            {
                if (i % RowsCount == 0) Locations.ColumnDefinitions.Add(new ColumnDefinition());

                var location = listLocations[i];

                //// JSA TTR: swap first 2 top level locations to get soil first (to the left) and the clean (to the right)
                //if (locationsToRender == topLevelLocations)
                //{
                //    if (i == 1)
                //        location = listLocations[0];
                //    if (i == 0 && listLocations.Count >= 1)
                //        location = listLocations[1];
                //}

                var stackPanel = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(4) };
                stackPanel.Children.Add(new Label() { Width = 0 });
                bool hasMultipleSubNodes = location != null && location.Children.Count > 1;

                TextBlock textBlock;
                if (location != null)
                {
                    String adjustedString = TruncateAndWrap(location.Name);

                    // Make both top level locations be written in bold
                    textBlock = new TextBlock() { Text = adjustedString, FontWeight = hasMultipleSubNodes ? FontWeights.Bold : (locationsToRender == topLevelLocations) ? FontWeights.Bold : FontWeights.Normal, TextAlignment = TextAlignment.Center };
                }
                else
                    textBlock = new TextBlock() { Text = ". . .", FontWeight = FontWeights.Bold, Opacity = 0.5 };
                stackPanel.Children.Add(textBlock);

                if (hasMultipleSubNodes)
                {
                    TextBlock arrowRight = new TextBlock() { Text = "…", FontWeight = FontWeights.UltraBold, Opacity = 0.5 };
                    stackPanel.Children.Add(arrowRight);
                }
                stackPanel.Children.Add(new Label() { Width = 0 });
                var locationButton = new RadioButton() { GroupName = "Location", Content = stackPanel, Margin = new Thickness(4), BorderThickness = new Thickness(0,0,8,8), Foreground = Brushes.White};
                locationButton.Resources.Add("location", location);

                locationButton.Click += Location_Click;
                Grid.SetColumn(locationButton, i / RowsCount); // Fill items vertically column by column
                Grid.SetRow(locationButton, i % RowsCount);
                Locations.Children.Add(locationButton);
            }

            /* Make the text in the buttons use the same fontsize and still be centered. There's no easy solution to this one but this kind of works. */
            Locations.UpdateLayout();
            double maxWidth = 0;
            foreach (var c in Locations.Children)
            {
                var b = (c as RadioButton);
                var sp = (b.Content as StackPanel);
                if (sp.ActualWidth > maxWidth)
                {
                    maxWidth = sp.ActualWidth;
                }
            }
            foreach (var c in Locations.Children)
            {
                var b = (c as RadioButton);
                var sp = (b.Content as StackPanel);
                foreach (var t in sp.Children)
                {
                    var lbl = (t as Label);
                    if (lbl != null)
                    {
                        lbl.MinWidth = (maxWidth - sp.ActualWidth) / 2;
                    }
                }
            }
            Locations.UpdateLayout();
        }


        /// <summary>
        /// Check string and adjust it, if needed, to be more optimal for the UI
        /// </summary>
        /// <param name="locationName">Input string to be adjusted</param>
        /// <returns>The adjusted string</returns>
        private String TruncateAndWrap(String locationName)
        {
            const int MAX_STRING_LEN = 50;          
            const int MAX_CUTAWAY_WORD_LEN = 15;    
            const int SWAP_THRESHOLD_LEN = 20;
            const String PREFIX = "..";

            String stringToReturn = locationName;

            /*  Algorithm
             *  
             *  Very long name would be 50 characters, e.g. "Original Sokos Hotel Olympia Garden St. Petersburg".
             *  
             *  - If the name is longger than MAX_STRING_LEN characters, truncate the extra stuff away (probably very, very rear case)
             *      * The more interesting info is typically at the end rather than in the beginning, 
             *        e.g. as in "Original Sokos Hotel Vaakuna Hämeenlinna"
             *  - If the name is longger than SWAP_THRESHOLD_LEN characters, wrap it into two halves:
             *      * if the name contains spaces, replace the space closest to the middle with newline
             *      * if the name does not contain any spaces, just add a newline into the middle (probably very, very, very rear case)
             * 
             */

            try
            {
                // In the very beginning, remove any potential spaces at ends of the string
                stringToReturn = stringToReturn.Trim(' ');

                // Cut very long names
                if (stringToReturn.Length > MAX_STRING_LEN)
                {
                    // Cut from the beginning, leaving space for prefix
                    stringToReturn = stringToReturn.Substring(stringToReturn.Length - MAX_STRING_LEN + PREFIX.Length);

                    // If string has more than one words and the 1st word (or what is left of it) is not very long, cut that away as well
                    if (stringToReturn.Split(' ').Length >= 2)
                    {
                        if (stringToReturn.Substring(0, stringToReturn.IndexOf(" ")).Length < MAX_CUTAWAY_WORD_LEN)
                        {
                            stringToReturn = stringToReturn.Substring(stringToReturn.IndexOf(" "));
                        }
                    }

                    // ..trim potential spaces and precede with prefix, "..Sokos Hotel Olympia Garden St. Petersburg"
                    stringToReturn = stringToReturn.Trim(' ');
                    stringToReturn = PREFIX + stringToReturn;
                }

                // Wrap long(ish) names to 2 lines
                if (stringToReturn.Length > SWAP_THRESHOLD_LEN)
                {
                    int targetLength = stringToReturn.Length / 2;
                    String[] words = stringToReturn.Split(' ');
                    String tempString = "";

                    /* "If this instance does not contain any of the characters in separator, or the count parameter is 1, 
                       the returned array consists of a single element that contains this instance." */

                    // We have no space(s) which would make a nice point for swapping - make just a brute force swap in the middle
                    if (words.Length <= 1)
                    {
                        tempString = stringToReturn.Substring(0, targetLength);
                        tempString = tempString + "\n" + stringToReturn.Substring(targetLength);

                        stringToReturn = tempString;
                    }
                    else // We do have space(s), use them
                    {
                        String previousLine = "", nextLineCandidate = "";

                        foreach (var word in words)
                        {
                            nextLineCandidate = previousLine + word;

                            // We found the sweet spot if the previous line has distance to the midpoint shorter (or equal) that the next candidate
                            if (Math.Abs(targetLength - previousLine.Length) <= Math.Abs(targetLength - nextLineCandidate.Length))
                            {
                                break;
                            }
                            else
                            {
                                previousLine = nextLineCandidate + " ";
                            }
                        }

                        // Swap lines - remove any spaces at the joint point
                        previousLine = previousLine.TrimEnd(' ');
                        nextLineCandidate = stringToReturn.Substring(previousLine.Length);
                        nextLineCandidate = nextLineCandidate.Trim(' ');

                        stringToReturn = previousLine + "\n" + nextLineCandidate;
                    }
                }

                return (stringToReturn);
            }

            catch (Exception ex)
            {
                /* 
                 * We do not want to throw an exception because of this, not that crusial from operation poit of view. 
                 * If there is a problem, just report it in the log and proceed.
                 */
                logger.Warn($"TruncateAndWrap() failed with string \"{locationName}\"");
                logger.Warn($"Got exception in TruncateAndWrap(): {ex.Message}");

                return locationName;
            }
        }


        private void KeyPressed(object sender, KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.P)
            {
                navigator.Navigate(SingletonNavigator.PageView.Ping);
            }
        }
    }
}
